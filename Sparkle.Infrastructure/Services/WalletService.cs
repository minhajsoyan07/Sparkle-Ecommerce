using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sparkle.Domain.Wallets;
using Sparkle.Domain.Orders;

namespace Sparkle.Infrastructure.Services;

public interface IWalletService
{
    Task<SellerWallet> CreateOrGetSellerWalletAsync(int sellerId);
    Task AddPendingBalanceAsync(int sellerId, decimal amount, decimal platformCommission, int orderId, string description);
    Task ClearPendingToAvailableAsync(int orderId, int? sellerId = null);
    Task ProcessRefundAsync(int orderId, decimal refundAmount);
    Task ProcessWithdrawalAsync(int withdrawalRequestId);
    Task<decimal> GetSellerAvailableBalanceAsync(int sellerId);
    Task<decimal> GetSellerPendingBalanceAsync(int sellerId);
    Task<List<WalletTransaction>> GetSellerTransactionsAsync(int sellerId, int pageSize = 50);
}

public class WalletService : IWalletService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<WalletService> _logger;

    public WalletService(ApplicationDbContext db, ILogger<WalletService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Get or create a seller wallet
    /// </summary>
    public async Task<SellerWallet> CreateOrGetSellerWalletAsync(int sellerId)
    {
        var wallet = await _db.SellerWallets
            .FirstOrDefaultAsync(w => w.SellerId == sellerId);

        if (wallet == null)
        {
            wallet = new SellerWallet
            {
                SellerId = sellerId,
                AvailableBalance = 0,
                PendingBalance = 0,
                TotalEarnings = 0,
                TotalWithdrawn = 0,
                IsActive = true,
                Currency = "BDT"
            };

            _db.SellerWallets.Add(wallet);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created new wallet for seller {SellerId}", sellerId);
        }

        return wallet;
    }

    /// <summary>
    /// Add amount to pending balance (escrow) when order is paid but not yet delivered
    /// Also updates Admin Wallet with commission
    /// </summary>
    /// <summary>
    /// Add amount to pending balance (escrow) when order is paid but not yet delivered
    /// Also updates Admin Wallet with commission
    /// </summary>
    /// <summary>
    /// Add amount to pending balance (escrow) when order is paid but not yet delivered
    /// Also updates Admin Wallet with commission
    /// </summary>
    public async Task AddPendingBalanceAsync(int sellerId, decimal amount, decimal platformCommission, int orderId, string description)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var wallet = await CreateOrGetSellerWalletAsync(sellerId);
                var balanceBefore = wallet.PendingBalance;

                wallet.PendingBalance += amount;

                // Record transaction for Seller
                var walletTransaction = new WalletTransaction
                {
                    SellerId = sellerId,
                    TransactionType = "Credit",
                    Source = "OrderEarning",
                    Amount = amount,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = wallet.PendingBalance,
                    ReferenceType = "Order",
                    ReferenceId = orderId.ToString(),
                    Status = "Pending",
                    Description = description ?? $"Order #{orderId} - Pending clearance",
                    TransactionDate = DateTime.UtcNow
                };

                // CRITICAL FIX: Add to the wallet's collection to ensure shadow FK (SellerWalletId) is set correctly
                wallet.Transactions.Add(walletTransaction);

                // --- Update Admin Wallet (Platform Earnings) ---
                if (platformCommission > 0)
                {
                    var adminWallet = await _db.AdminWallets.FirstOrDefaultAsync();
                    if (adminWallet == null)
                    {
                        // Create if not exists (safeguard)
                        adminWallet = new AdminWallet
                        {
                            TotalCommissionEarned = 0,
                            CurrentBalance = 0,
                            TotalRefunded = 0,
                            TotalPayoutsToSellers = 0,
                            Currency = "BDT",
                            CreatedAt = DateTime.UtcNow,
                            LastUpdated = DateTime.UtcNow
                        };
                        _db.AdminWallets.Add(adminWallet);
                    }

                    adminWallet.TotalCommissionEarned += platformCommission;
                    adminWallet.CurrentBalance += platformCommission;
                    adminWallet.ThisMonthCommission += platformCommission;
                    adminWallet.LastUpdated = DateTime.UtcNow;

                    // Log Admin Transaction 
                    // Note: AdminTransaction currently has no FK to AdminWallet in the schema, so adding to DbSet is correct.
                    var adminTx = new AdminTransaction
                    {
                        Amount = platformCommission,
                        BalanceBefore = adminWallet.CurrentBalance - platformCommission,
                        BalanceAfter = adminWallet.CurrentBalance,
                        TransactionType = "Commission",
                        ReferenceType = "Order",
                        ReferenceId = orderId.ToString(),
                        Description = $"Commission from Order #{orderId}",
                        TransactionDate = DateTime.UtcNow
                    };
                    _db.AdminTransactions.Add(adminTx);

                    _logger.LogInformation("Added platform commission: {Commission} BDT (Order: {OrderId})", platformCommission, orderId);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Added pending balance for seller {SellerId}: {Amount} BDT (Order: {OrderId})",
                    sellerId, amount, orderId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to add pending balance for seller {SellerId}", sellerId);
                throw;
            }
        });
    }

    /// <summary>
    /// Move funds from pending to available when order is delivered
    /// </summary>
    public async Task ClearPendingToAvailableAsync(int orderId, int? sellerId = null)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Find all pending transactions for this order
                var query = _db.WalletTransactions
                    .Where(t => t.ReferenceType == "Order" &&
                               t.ReferenceId == orderId.ToString() &&
                               t.Status == "Pending" &&
                               t.SellerId != null);

                if (sellerId.HasValue)
                {
                    query = query.Where(t => t.SellerId == sellerId.Value);
                }

                var pendingTransactions = await query.ToListAsync();

                if (!pendingTransactions.Any())
                {
                    _logger.LogWarning("No pending transactions found for order {OrderId}", orderId);
                    await transaction.CommitAsync();
                    return; // Already processed or no transactions
                }

                foreach (var pendingTx in pendingTransactions)
                {
                    var sellerId = pendingTx.SellerId!.Value;
                    var wallet = await CreateOrGetSellerWalletAsync(sellerId);

                    // Move from pending to available
                    wallet.PendingBalance -= pendingTx.Amount;
                    wallet.AvailableBalance += pendingTx.Amount;
                    wallet.TotalEarnings += pendingTx.Amount;

                    // Mark pending transaction as completed
                    pendingTx.Status = "Completed";

                    // Create a new transaction for the clearance
                    var clearanceTransaction = new WalletTransaction
                    {
                        SellerId = sellerId,
                        TransactionType = "Credit",
                        Source = "OrderEarning",
                        Amount = pendingTx.Amount,
                        BalanceBefore = wallet.AvailableBalance - pendingTx.Amount,
                        BalanceAfter = wallet.AvailableBalance,
                        ReferenceType = "Order",
                        ReferenceId = orderId.ToString(),
                        Status = "Completed",
                        Description = $"Order #{orderId} delivered - Funds released to available balance",
                        TransactionDate = DateTime.UtcNow
                    };

                    _db.WalletTransactions.Add(clearanceTransaction);

                    _logger.LogInformation(
                        "Released pending balance to available for seller {SellerId}: {Amount} BDT (Order: {OrderId})",
                        sellerId, pendingTx.Amount, orderId);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to clear pending balance for order {OrderId}", orderId);
                throw;
            }
        });
    }

    /// <summary>
    /// Process refund by reversing wallet transactions
    /// </summary>
    public async Task ProcessRefundAsync(int orderId, decimal refundAmount)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                // Find all completed transactions for this order
                var orderTransactions = await _db.WalletTransactions
                    .Where(t => t.ReferenceType == "Order" &&
                               t.ReferenceId == orderId.ToString() &&
                               t.SellerId != null)
                    .ToListAsync();

                if (!orderTransactions.Any())
                {
                    _logger.LogWarning("No transactions found for order {OrderId} refund", orderId);
                    await transaction.CommitAsync();
                    return;
                }

                // Group by seller (in case order has items from multiple sellers)
                var sellerGroups = orderTransactions.GroupBy(t => t.SellerId!.Value);

                foreach (var sellerGroup in sellerGroups)
                {
                    var sellerId = sellerGroup.Key;
                    var wallet = await CreateOrGetSellerWalletAsync(sellerId);

                    var sellerRefundAmount = sellerGroup.Sum(t => t.Amount);

                    // Deduct from available balance (or pending if not yet cleared)
                    var deductFromAvailable = Math.Min(wallet.AvailableBalance, sellerRefundAmount);
                    var deductFromPending = sellerRefundAmount - deductFromAvailable;

                    wallet.AvailableBalance -= deductFromAvailable;
                    wallet.PendingBalance -= deductFromPending;
                    wallet.TotalEarnings -= sellerRefundAmount;

                    // Record refund transaction
                    var refundTransaction = new WalletTransaction
                    {
                        SellerId = sellerId,
                        TransactionType = "Debit",
                        Source = "Refund",
                        Amount = sellerRefundAmount,
                        BalanceBefore = wallet.AvailableBalance + deductFromAvailable,
                        BalanceAfter = wallet.AvailableBalance,
                        ReferenceType = "Order",
                        ReferenceId = orderId.ToString(),
                        Status = "Completed",
                        Description = $"Refund for Order #{orderId}",
                        TransactionDate = DateTime.UtcNow
                    };

                    _db.WalletTransactions.Add(refundTransaction);

                    _logger.LogInformation(
                        "Processed refund for seller {SellerId}: {Amount} BDT (Order: {OrderId})",
                        sellerId, sellerRefundAmount, orderId);
                }

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process refund for order {OrderId}", orderId);
                throw;
            }
        });
    }

    /// <summary>
    /// Process approved withdrawal request
    /// </summary>
    public async Task ProcessWithdrawalAsync(int withdrawalRequestId)
    {
        var strategy = _db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var withdrawalRequest = await _db.WithdrawalRequests
                    .FirstOrDefaultAsync(w => w.Id == withdrawalRequestId);

                if (withdrawalRequest == null)
                {
                    throw new InvalidOperationException($"Withdrawal request {withdrawalRequestId} not found");
                }

                if (withdrawalRequest.Status != "Approved")
                {
                    throw new InvalidOperationException($"Withdrawal request {withdrawalRequestId} is not in approved state");
                }

                var wallet = await CreateOrGetSellerWalletAsync(withdrawalRequest.SellerId);

                if (wallet.AvailableBalance < withdrawalRequest.Amount)
                {
                    throw new InvalidOperationException($"Insufficient balance for withdrawal. Available: {wallet.AvailableBalance}, Requested: {withdrawalRequest.Amount}");
                }

                // Deduct from available balance
                wallet.AvailableBalance -= withdrawalRequest.Amount;
                wallet.TotalWithdrawn += withdrawalRequest.Amount;

                // Record withdrawal transaction
                var withdrawalTransaction = new WalletTransaction
                {
                    SellerId = withdrawalRequest.SellerId,
                    TransactionType = "Debit",
                    Source = "Withdrawal",
                    Amount = withdrawalRequest.Amount,
                    BalanceBefore = wallet.AvailableBalance + withdrawalRequest.Amount,
                    BalanceAfter = wallet.AvailableBalance,
                    ReferenceType = "WithdrawalRequest",
                    ReferenceId = withdrawalRequestId.ToString(),
                    Status = "Completed",
                    Description = $"Withdrawal to {withdrawalRequest.BankName} - {withdrawalRequest.AccountNumber}",
                    TransactionDate = DateTime.UtcNow
                };

                _db.WalletTransactions.Add(withdrawalTransaction);

                // Update withdrawal request status
                withdrawalRequest.Status = "Processed";
                withdrawalRequest.ProcessedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation(
                    "Processed withdrawal for seller {SellerId}: {Amount} BDT (Request: {RequestId})",
                    withdrawalRequest.SellerId, withdrawalRequest.Amount, withdrawalRequestId);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Failed to process withdrawal request {RequestId}", withdrawalRequestId);
                throw;
            }
        });
    }

    /// <summary>
    /// Get seller available balance
    /// </summary>
    public async Task<decimal> GetSellerAvailableBalanceAsync(int sellerId)
    {
        var wallet = await _db.SellerWallets
            .FirstOrDefaultAsync(w => w.SellerId == sellerId);

        return wallet?.AvailableBalance ?? 0;
    }

    /// <summary>
    /// Get seller pending balance
    /// </summary>
    public async Task<decimal> GetSellerPendingBalanceAsync(int sellerId)
    {
        var wallet = await _db.SellerWallets
            .FirstOrDefaultAsync(w => w.SellerId == sellerId);

        return wallet?.PendingBalance ?? 0;
    }

    /// <summary>
    /// Get seller transaction history
    /// </summary>
    public async Task<List<WalletTransaction>> GetSellerTransactionsAsync(int sellerId, int pageSize = 50)
    {
        return await _db.WalletTransactions
            .Where(t => t.SellerId == sellerId)
            .OrderByDescending(t => t.TransactionDate)
            .Take(pageSize)
            .ToListAsync();
    }
}
