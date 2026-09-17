using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Infrastructure.Services;
using Sparkle.Domain.Wallets;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin/wallets")]
public class WalletsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWalletService _walletService;
    private readonly ILogger<WalletsController> _logger;

    public WalletsController(ApplicationDbContext context, IWalletService walletService, ILogger<WalletsController> logger)
    {
        _context = context;
        _walletService = walletService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // Get summary stats
        var totalUserBalance = await _context.UserWallets.SumAsync(w => w.Balance);
        var totalSellerAvailable = await _context.SellerWallets.SumAsync(w => w.AvailableBalance);
        var totalSellerPending = await _context.SellerWallets.SumAsync(w => w.PendingBalance);
        var pendingWithdrawals = await _context.WithdrawalRequests.CountAsync(w => w.Status == "Pending");
        var totalWithdrawn = await _context.SellerWallets.SumAsync(w => w.TotalWithdrawn);

        var adminWallet = await _context.AdminWallets.FirstOrDefaultAsync();

        ViewBag.TotalUserBalance = totalUserBalance;
        ViewBag.TotalSellerAvailable = totalSellerAvailable;
        ViewBag.TotalSellerPending = totalSellerPending;
        ViewBag.PendingWithdrawals = pendingWithdrawals;
        ViewBag.TotalWithdrawn = totalWithdrawn;
        
        // Admin Wallet Stats
        ViewBag.PlatformTotalCommission = adminWallet?.TotalCommissionEarned ?? 0;
        ViewBag.PlatformCurrentBalance = adminWallet?.CurrentBalance ?? 0;
        ViewBag.PlatformThisMonth = adminWallet?.ThisMonthCommission ?? 0;

        var recentWithdrawals = await _context.WithdrawalRequests
            .Include(w => w.Seller)
            .OrderByDescending(w => w.RequestDate)
            .Take(20)
            .ToListAsync();

        var recentAdminTransactions = await _context.AdminTransactions
            .OrderByDescending(t => t.TransactionDate)
            .Take(20)
            .ToListAsync();

        ViewBag.RecentAdminTransactions = recentAdminTransactions;

        return View(recentWithdrawals);
    }
    
    [HttpPost("approve/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveWithdrawal(int id)
    {
        var withdrawal = await _context.WithdrawalRequests
            .Include(w => w.Seller)
            .FirstOrDefaultAsync(w => w.Id == id);
        
        if (withdrawal == null)
        {
            TempData["Error"] = "Withdrawal request not found.";
            return RedirectToAction(nameof(Index));
        }
        
        if (withdrawal.Status != "Pending")
        {
            TempData["Warning"] = $"Withdrawal request is already {withdrawal.Status}.";
            return RedirectToAction(nameof(Index));
        }
        
        // Check if seller has sufficient balance
        var availableBalance = await _walletService.GetSellerAvailableBalanceAsync(withdrawal.SellerId);
        if (availableBalance < withdrawal.Amount)
        {
            TempData["Error"] = $"Insufficient seller balance. Available: ৳{availableBalance:N2}, Requested: ৳{withdrawal.Amount:N2}";
            return RedirectToAction(nameof(Index));
        }
        
        withdrawal.Status = "Approved";
        withdrawal.ProcessedBy = User.Identity?.Name ?? "Admin";
        withdrawal.ProcessedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        TempData["Success"] = $"Withdrawal request approved for {withdrawal.Seller.ShopName}. Amount: ৳{withdrawal.Amount:N2}";
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost("reject/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectWithdrawal(int id, string reason)
    {
        var withdrawal = await _context.WithdrawalRequests
            .Include(w => w.Seller)
            .FirstOrDefaultAsync(w => w.Id == id);
        
        if (withdrawal == null)
        {
            TempData["Error"] = "Withdrawal request not found.";
            return RedirectToAction(nameof(Index));
        }
        
        if (withdrawal.Status != "Pending")
        {
            TempData["Warning"] = $"Withdrawal request is already {withdrawal.Status }.";
            return RedirectToAction(nameof(Index));
        }
        
        withdrawal.Status = "Rejected";
        withdrawal.RejectionReason = reason;
        withdrawal.ProcessedBy = User.Identity?.Name ?? "Admin";
        withdrawal.ProcessedAt = DateTime.UtcNow;
        
        await _context.SaveChangesAsync();
        
        TempData["Info"] = $"Withdrawal request rejected for {withdrawal.Seller.ShopName}.";
        return RedirectToAction(nameof(Index));
    }
    
    [HttpPost("process/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcessWithdrawal(int id, string transactionReference)
    {
        var withdrawal = await _context.WithdrawalRequests
            .Include(w => w.Seller)
            .FirstOrDefaultAsync(w => w.Id == id);
        
        if (withdrawal == null)
        {
            TempData["Error"] = "Withdrawal request not found.";
            return RedirectToAction(nameof(Index));
        }
        
        if (withdrawal.Status != "Approved")
        {
            TempData["Warning"] = "Only approved withdrawal requests can be processed.";
            return RedirectToAction(nameof(Index));
        }
        
        try
        {
            withdrawal.TransactionReference = transactionReference;
            await _walletService.ProcessWithdrawalAsync(withdrawal.Id);
            
            TempData["Success"] = $"Withdrawal processed successfully for {withdrawal.Seller.ShopName}. Reference: {transactionReference}";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process withdrawal {WithdrawalId}", id);
            TempData["Error"] = $"Failed to process withdrawal: {ex.Message}";
        }
        
        return RedirectToAction(nameof(Index));
    }
}
