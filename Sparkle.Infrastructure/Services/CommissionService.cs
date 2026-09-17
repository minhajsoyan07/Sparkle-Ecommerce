using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Configuration;
using Sparkle.Domain.Orders;
using System.Text.Json;
using Sparkle.Infrastructure;
using Microsoft.Extensions.Logging;

namespace Sparkle.Infrastructure.Services;

public interface ICommissionService
{
    Task ProcessOrderCommissionAsync(int orderId);
    Task<decimal> GetSellerAvailableBalanceAsync(int sellerId);
    Task<decimal> GetAdminTotalCommissionsAsync();
}

public class CommissionService : ICommissionService
{
    private readonly ApplicationDbContext _db;
    private readonly IWalletService _walletService;
    private readonly ILogger<CommissionService> _logger;

    public CommissionService(ApplicationDbContext db, IWalletService walletService, ILogger<CommissionService> logger)
    {
        _db = db;
        _walletService = walletService;
        _logger = logger;
    }

    public async Task ProcessOrderCommissionAsync(int orderId)
    {
        try 
        {
            var order = await _db.Orders
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.ProductVariant)
                .ThenInclude(pv => pv!.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                _logger.LogWarning("Commission processing: Order {OrderId} not found", orderId);
                return;
            }

            var config = await _db.CommissionConfigs
                .Where(c => c.IsActive && c.EffectiveFrom <= DateTime.UtcNow)
                .OrderByDescending(c => c.EffectiveFrom)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                 _logger.LogInformation("No active CommissionConfig found. Using default 15%.");
                 config = new CommissionConfig { GlobalRate = 15.0m };
            }

            var sellerRates = JsonSerializer.Deserialize<Dictionary<string, decimal>>(config.SellerRates ?? "{}") ?? new();
            var categoryRates = JsonSerializer.Deserialize<Dictionary<string, decimal>>(config.CategoryRates ?? "{}") ?? new();

            var sellerGroups = order.OrderItems
                .Where(i => i.ProductVariant != null && i.ProductVariant.Product != null)
                .GroupBy(i => i.ProductVariant!.Product.SellerId);

            foreach (var group in sellerGroups)
            {
                var sellerId = group.Key;
                if (!sellerId.HasValue) continue;

                decimal sellerTotal = group.Sum(i => i.TotalPrice);
                decimal commissionRate = config.GlobalRate;

                // Priority 1: Seller Override
                if (sellerRates.TryGetValue(sellerId.Value.ToString(), out var sRate))
                {
                    commissionRate = sRate;
                }
                else 
                {
                    // Priority 2: Category Rate
                    var categoryId = group.First().ProductVariant!.Product.CategoryId;
                    if (categoryRates.TryGetValue(categoryId.ToString(), out var cRate))
                    {
                        commissionRate = cRate;
                    }
                }

                decimal commissionAmount = Math.Round(sellerTotal * (commissionRate / 100m), 2);
                decimal sellerNetEarning = sellerTotal - commissionAmount;

                // UPDATE ORDER ITEMS
                foreach (var item in group)
                {
                   // Pro-rate commission per item
                   decimal itemRatio = item.TotalPrice / sellerTotal;
                   decimal itemCommission = Math.Round(commissionAmount * itemRatio, 2);
                   decimal itemEarning = item.TotalPrice - itemCommission;

                   item.PlatformCommissionRate = commissionRate;
                   item.PlatformCommissionAmount = itemCommission;
                   item.SellerEarning = itemEarning;
                }

                await _walletService.AddPendingBalanceAsync(
                    sellerId.Value, 
                    sellerNetEarning, 
                    commissionAmount, 
                    orderId, 
                    $"Commission ({commissionRate}%) from Order {order.OrderNumber}");
            }
            
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order commission for order {OrderId}", orderId);
            // We don't throw here to avoid failing the whole checkout if commission logging fails,
            // though in a real system this should be robust/queued.
        }
    }

    public async Task<decimal> GetSellerAvailableBalanceAsync(int sellerId)
    {
        return await _walletService.GetSellerAvailableBalanceAsync(sellerId);
    }

    public async Task<decimal> GetAdminTotalCommissionsAsync()
    {
        var adminWallet = await _db.AdminWallets.FirstOrDefaultAsync();
        return adminWallet?.TotalCommissionEarned ?? 0;
    }
}
