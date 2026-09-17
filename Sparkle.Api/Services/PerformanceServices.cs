using Sparkle.Domain.Sellers;
using Sparkle.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;

namespace Sparkle.Api.Services;

public interface ICachingService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
}

public class RedisCachingService : ICachingService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<RedisCachingService> _logger;

    public RedisCachingService(
        IDistributedCache cache,
        ILogger<RedisCachingService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var data = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(data))
                return default;

            return System.Text.Json.JsonSerializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting cache key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        try
        {
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = expiry ?? TimeSpan.FromMinutes(30)
            };

            var data = System.Text.Json.JsonSerializer.Serialize(value, new System.Text.Json.JsonSerializerOptions
            {
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
            });

            await _cache.SetStringAsync(key, data, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key {Key}", key);
        }
    }

    public async Task RemoveByPrefixAsync(string prefix)
    {
        _logger.LogInformation("Cache prefix removal requested: {Prefix} (not supported in current implementation)", prefix);
        // Note: Distributed cache doesn't support pattern-based deletion by default
        // Would require Redis-specific implementation or key tracking
        await Task.CompletedTask;
    }
}

// Seller Performance Scoring Service
public interface ISellerPerformanceService
{
    Task CalculateAndUpdateScoresAsync();
    Task<SellerPerformanceScore?> GetSellerScoreAsync(int sellerId);
}

public class SellerPerformanceService : ISellerPerformanceService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<SellerPerformanceService> _logger;

    public SellerPerformanceService(ApplicationDbContext db, ILogger<SellerPerformanceService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task CalculateAndUpdateScoresAsync()
    {
        var sellers = await _db.Sellers
            .Where(s => s.Status == SellerStatus.Approved)
            .ToListAsync();

        foreach (var seller in sellers)
        {
            try
            {
                var score = await CalculateScoreAsync(seller.Id);
                
                // Update or create score record
                var existing = await _db.SellerPerformanceScores
                    .FirstOrDefaultAsync(s => s.SellerId == seller.Id);

                if (existing != null)
                {
                    existing.OverallScore = score.OverallScore;
                    existing.SalesScore = score.SalesScore;
                    existing.QualityScore = score.QualityScore;
                    existing.ResponseScore = score.ResponseScore;
                    existing.DeliveryScore = score.DeliveryScore;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    _db.SellerPerformanceScores.Add(score);
                }

                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calculating score for seller {SellerId}", seller.Id);
            }
        }

        _logger.LogInformation("Performance scores updated for {Count} sellers", sellers.Count);
    }

    public async Task<SellerPerformanceScore?> GetSellerScoreAsync(int sellerId)
    {
        return await _db.SellerPerformanceScores
            .FirstOrDefaultAsync(s => s.SellerId == sellerId);
    }

    private async Task<SellerPerformanceScore> CalculateScoreAsync(int sellerId)
    {
        var now = DateTime.UtcNow;
        var threeMonthsAgo = now.AddMonths(-3);

        // Sales metrics (last 3 months)
        var recentOrders = await _db.Orders
            .Where(o => o.SellerId == sellerId && o.CreatedAt >= threeMonthsAgo)
            .ToListAsync();

        var totalOrders = recentOrders.Count;
        var completedOrders = recentOrders.Count(o => o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered);
        var totalRevenue = recentOrders.Sum(o => o.TotalAmount);

        // Quality metrics (ratings)
        var avgRating = await _db.Sellers
            .Where(s => s.Id == sellerId)
            .Select(s => s.Rating)
            .FirstOrDefaultAsync();

        // Response time (support tickets)
        var tickets = await _db.SupportTickets
            .Where(t => t.SellerId == sellerId && t.CreatedAt >= threeMonthsAgo && t.FirstResponseAt.HasValue)
            .ToListAsync();

        var avgResponseMinutes = tickets.Any()
            ? tickets.Average(t => (t.FirstResponseAt!.Value - t.CreatedAt).TotalMinutes)
            : 0;

        // Delivery performance
        var shipments = await _db.Shipments
            .Where(s => s.Order.SellerId == sellerId && s.CreatedAt >= threeMonthsAgo && s.DeliveredAt.HasValue)
            .ToListAsync();

        var onTimeDeliveries = shipments.Count(s => s.DeliveredAt <= s.EstimatedDeliveryDate);
        var deliveryRate = shipments.Any() ? (double)onTimeDeliveries / shipments.Count * 100 : 0;

        // Calculate scores (0-100 scale)
        var salesScore = CalculateSalesScore(totalOrders, totalRevenue);
        var qualityScore = (decimal)avgRating * 20; // 5-star to 100-point
        var responseScore = CalculateResponseScore(avgResponseMinutes);
        var deliveryScore = (decimal)deliveryRate;

        // Overall weighted score
        var overallScore = (salesScore * 0.3m) + (qualityScore * 0.3m) + (responseScore * 0.2m) + (deliveryScore * 0.2m);

        return new SellerPerformanceScore
        {
            SellerId = sellerId,
            OverallScore = overallScore,
            SalesScore = salesScore,
            QualityScore = qualityScore,
            ResponseScore = responseScore,
            DeliveryScore = deliveryScore,
            TotalOrders = totalOrders,
            CompletedOrders = completedOrders,
            TotalRevenue = totalRevenue,
            AverageResponseTimeMinutes = (int)avgResponseMinutes,
            OnTimeDeliveryRate = (decimal)deliveryRate,
            Period = "Last 3 Months",
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    private decimal CalculateSalesScore(int orders, decimal revenue)
    {
        // Scale: 0-20 orders = 0-50 points, 20-100 orders = 50-100 points
        if (orders <= 20)
            return (decimal)orders / 20 * 50;
        
        return 50 + Math.Min((decimal)(orders - 20) / 80 * 50, 50);
    }

    private decimal CalculateResponseScore(double avgMinutes)
    {
        // Fast response = higher score
        // < 30 min = 100, 30-60 min = 80, 1-4 hours = 60, 4-24 hours = 40, > 24 hours = 20
        if (avgMinutes <= 30) return 100;
        if (avgMinutes <= 60) return 80;
        if (avgMinutes <= 240) return 60;
        if (avgMinutes <= 1440) return 40;
        return 20;
    }
}
