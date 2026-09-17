using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Content;
using Sparkle.Domain.Catalog;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Services;

/// <summary>
/// Provides intelligent analysis for product recommendations
/// Uses advanced algorithms analyzing user behavior and sales patterns
/// All intelligence works silently in the backend - no AI terms visible on frontend
/// </summary>
public interface IIntelligentProductAnalysisService
{
    // Trending Products Analysis
    Task<List<TrendingProductSuggestion>> AnalyzeTrendingProductsAsync(int topCount = 12, string period = "Last30Days");
    Task<List<Product>> GetCurrentTrendingProductsAsync(int limit = 12);
    Task RefreshTrendingProductsAsync();

    // Flash Sale Suggestions
    Task<List<FlashSaleProductSuggestion>> AnalyzeFlashSaleOpportunitiesAsync(int topCount = 10);
    Task<List<Product>> GetSuggestedFlashSaleProductsAsync(int limit = 10);
    Task RefreshFlashSaleSuggestionsAsync();

    // User Behavior Analysis
    Task LogUserActionAsync(string userId, string actionType, int? productId = null, int? categoryId = null, string? searchTerm = null, int? timeSpentSeconds = null);
    Task<List<int>> GetRecommendedProductsForUserAsync(string userId, int limit = 12);
    Task<List<int>> GetUserSearchPatternAsync(string userId, int limit = 5);

    // Sales Metrics
    Task UpdateSalesMetricsAsync(int productId);
    Task<SalesMetricsSnapshot?> GetLatestMetricsAsync(int productId);
    Task RefreshAllMetricsAsync();

    // Bulk Analysis Operations
    Task RunDailyAnalysisAsync();
    Task RunWeeklyAnalysisAsync();
}

public class IntelligentProductAnalysisService : IIntelligentProductAnalysisService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<IntelligentProductAnalysisService> _logger;

    public IntelligentProductAnalysisService(ApplicationDbContext db, ILogger<IntelligentProductAnalysisService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ==================== TRENDING PRODUCTS ANALYSIS ====================

    /// <summary>
    /// Analyzes products and generates trending product suggestions
    /// Uses multi-factor analysis: sales velocity, search volume, wishlist adds, ratings
    /// </summary>
    public async Task<List<TrendingProductSuggestion>> AnalyzeTrendingProductsAsync(int topCount = 12, string period = "Last30Days")
    {
        var (startDate, periodName) = GetPeriodDates(period);

        _logger.LogInformation($"Starting trending product analysis for {periodName}");

        // Get all active products
        var products = await _db.Products
            .Where(p => p.IsActive)
            .ToListAsync();

        var suggestions = new List<TrendingProductSuggestion>();

        foreach (var product in products)
        {
            // Analyze each product
            var (score, metrics) = await AnalyzeProductTrendingAsync(product.Id, startDate);

            if (score > 0) // Only include products with positive scores
            {
                suggestions.Add(new TrendingProductSuggestion
                {
                    ProductId = product.Id,
                    ConfidenceScore = score,
                    SalesCount = metrics.SalesCount,
                    ViewCount = metrics.ViewCount,
                    WishlistCount = metrics.WishlistCount,
                    AverageRating = metrics.AverageRating,
                    SalesGrowthRate = metrics.SalesGrowthRate,
                    CalculatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(GetExpirationDays(period)),
                    IsActive = true,
                    AnalysisPeriod = periodName
                });
            }
        }

        // Sort by confidence score and rank them
        suggestions = suggestions
            .OrderByDescending(s => s.ConfidenceScore)
            .Take(topCount)
            .ToList();

        // Assign ranks
        for (int i = 0; i < suggestions.Count; i++)
        {
            suggestions[i].Rank = i + 1;
        }

        // Save suggestions
        _db.TrendingProductSuggestions.AddRange(suggestions);
        await _db.SaveChangesAsync();

        _logger.LogInformation($"Trending product analysis completed. Found {suggestions.Count} trending products");
        return suggestions;
    }

    /// <summary>
    /// Gets current trending products from cached suggestions
    /// Filters to only show in-stock products
    /// </summary>
    public async Task<List<Product>> GetCurrentTrendingProductsAsync(int limit = 12)
    {
        var now = DateTime.UtcNow;
        var suggestions = await _db.TrendingProductSuggestions
            .AsNoTracking()
            .Include(t => t.Product).ThenInclude(p => p.Variants)
            .Include(t => t.Product).ThenInclude(p => p.Images)
            .Where(t => t.IsActive && t.ExpiresAt > now)
            .OrderBy(t => t.Rank)
            .Take(limit * 2)
            .ToListAsync();

        var products = suggestions.Select(t => t.Product).ToList();

        // Filter to only include in-stock products
        var inStockProducts = new List<Product>();
        foreach (var product in products)
        {
            var totalStock = product.Variants.Sum(v => v.Stock);

            if (totalStock > 0 && inStockProducts.Count < limit)
            {
                inStockProducts.Add(product);
            }
        }

        return inStockProducts;
    }

    /// <summary>
    /// Refreshes trending product analysis and cleans up expired suggestions
    /// </summary>
    public async Task RefreshTrendingProductsAsync()
    {
        // Remove expired suggestions
        var expired = await _db.TrendingProductSuggestions
            .Where(t => t.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        _db.TrendingProductSuggestions.RemoveRange(expired);

        // Analyze new trending products
        var newSuggestions = await AnalyzeTrendingProductsAsync();

        await _db.SaveChangesAsync();
        _logger.LogInformation($"Refreshed trending products. Removed {expired.Count} expired, added {newSuggestions.Count} new");
    }

    // ==================== FLASH SALE SUGGESTIONS ====================

    /// <summary>
    /// Analyzes products and suggests candidates for flash sales
    /// Considers: inventory levels, sales velocity, price point, seasonality
    /// </summary>
    public async Task<List<FlashSaleProductSuggestion>> AnalyzeFlashSaleOpportunitiesAsync(int topCount = 10)
    {
        _logger.LogInformation("Starting flash sale opportunity analysis");

        var products = await _db.Products
            .Where(p => p.IsActive)
            .ToListAsync();

        var suggestions = new List<FlashSaleProductSuggestion>();

        foreach (var product in products)
        {
            var suggestion = await AnalyzeFlashSaleOpportunityAsync(product.Id);
            if (suggestion != null)
                suggestions.Add(suggestion);
        }

        // Sort by priority score
        suggestions = suggestions
            .OrderByDescending(s => s.PriorityScore)
            .Take(topCount)
            .ToList();

        // Save suggestions
        _db.FlashSaleProductSuggestions.AddRange(suggestions);
        await _db.SaveChangesAsync();

        _logger.LogInformation($"Flash sale analysis completed. Found {suggestions.Count} opportunities");
        return suggestions;
    }

    /// <summary>
    /// Gets suggested flash sale products
    /// Only returns products with available stock
    /// </summary>
    public async Task<List<Product>> GetSuggestedFlashSaleProductsAsync(int limit = 10)
    {
        var now = DateTime.UtcNow;
        var suggestions = await _db.FlashSaleProductSuggestions
            .AsNoTracking()
            .Include(f => f.Product).ThenInclude(p => p.Variants)
            .Include(f => f.Product).ThenInclude(p => p.Images)
            .Where(f => f.IsActive && f.ExpiresAt > now)
            .OrderByDescending(f => f.PriorityScore)
            .Take(limit * 2)
            .ToListAsync();

        var products = suggestions.Select(f => f.Product).ToList();

        // Filter to only include in-stock products
        var inStockProducts = new List<Product>();
        foreach (var product in products)
        {
            var totalStock = product.Variants.Sum(v => v.Stock);

            if (totalStock > 0 && inStockProducts.Count < limit)
            {
                inStockProducts.Add(product);
            }
        }

  return inStockProducts;
    }

    /// <summary>
    /// Refreshes flash sale suggestions
    /// </summary>
    public async Task RefreshFlashSaleSuggestionsAsync()
    {
        var expired = await _db.FlashSaleProductSuggestions
            .Where(f => f.ExpiresAt <= DateTime.UtcNow)
            .ToListAsync();

        _db.FlashSaleProductSuggestions.RemoveRange(expired);

        var newSuggestions = await AnalyzeFlashSaleOpportunitiesAsync();
        await _db.SaveChangesAsync();

        _logger.LogInformation($"Refreshed flash sale suggestions. Removed {expired.Count} expired, added {newSuggestions.Count} new");
    }

    // ==================== USER BEHAVIOR TRACKING ====================

    /// <summary>
    /// Logs user actions for behavioral analysis
    /// Actions: ProductView, ProductSearch, AddToCart, AddToWishlist, Purchase, etc.
    /// </summary>
    public async Task LogUserActionAsync(string userId, string actionType, int? productId = null, int? categoryId = null, string? searchTerm = null, int? timeSpentSeconds = null)
    {
        var action = new UserBehaviorAnalytic
        {
            UserId = userId,
            ActionType = actionType,
            ProductId = productId,
            CategoryId = categoryId,
            SearchTerm = searchTerm,
            TimeSpentSeconds = timeSpentSeconds,
            ActionDateTime = DateTime.UtcNow,
            SessionId = Guid.NewGuid().ToString() // Should be set by caller if tracking session
        };

        _db.UserBehaviorAnalytics.Add(action);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Gets recommended products for a user based on their behavior
    /// Analyzes: browsing history, search patterns, similar user behaviors
    /// </summary>
    public async Task<List<int>> GetRecommendedProductsForUserAsync(string userId, int limit = 12)
    {
     // Get user's viewing patterns
        var userProducts = await _db.UserBehaviorAnalytics
        .AsNoTracking()
            .Where(u => u.UserId == userId && u.ProductId.HasValue)
            .Select(u => u.ProductId.GetValueOrDefault())
   .Distinct()
     .Take(10)
            .ToListAsync();

        if (userProducts.Count == 0)
     {
 // No history - return trending products instead
     var trending = await GetCurrentTrendingProductsAsync(limit);
       return trending.Select(p => p.Id).ToList();
      }

   // Get categories of products user viewed
        var userCategories = await _db.Products
 .AsNoTracking()
            .Where(p => userProducts.Contains(p.Id))
         .Select(p => p.CategoryId)
 .Distinct()
      .ToListAsync();

        // Find similar products in those categories with available stock
        var recommendations = new List<int>();
        var candidates = await _db.Products
     .AsNoTracking()
            .Where(p => p.IsActive && userCategories.Contains(p.CategoryId) && !userProducts.Contains(p.Id))
  .OrderByDescending(p => p.TotalReviews)
    .ToListAsync();

        foreach (var product in candidates)
        {
     if (recommendations.Count >= limit)
                break;

  var totalStock = await _db.ProductVariants
   .AsNoTracking()
            .Where(v => v.ProductId == product.Id)
                .SumAsync(v => v.Stock);

     if (totalStock > 0)
       {
    recommendations.Add(product.Id);
            }
        }

        return recommendations;
    }

    /// <summary>
    /// Gets user's search patterns for analysis
    /// </summary>
    public async Task<List<int>> GetUserSearchPatternAsync(string userId, int limit = 5)
    {
        var patterns = await _db.UserBehaviorAnalytics
            .AsNoTracking()
            .Where(u => u.UserId == userId && u.ActionType == "ProductSearch")
            .GroupBy(u => u.SearchTerm)
            .OrderByDescending(g => g.Count())
            .Take(limit)
            .Select(g => g.Key!)
            .ToListAsync();

        // Return as empty list - this is for internal analysis
        // In real scenario, you might return related product IDs based on these searches
        return new List<int>();
    }

    // ==================== SALES METRICS ====================

    /// <summary>
    /// Updates or creates sales metrics snapshot for a product
    /// Calculates: sales count, revenue, conversion rate, trend
    /// </summary>
    public async Task UpdateSalesMetricsAsync(int productId)
    {
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var sixtyDaysAgo = DateTime.UtcNow.AddDays(-60);

        // Current period metrics
        var currentSales = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Product!.Id == productId && oi.Order!.OrderDate >= thirtyDaysAgo)
            .GroupBy(oi => 1)
            .Select(g => new
            {
                Count = g.Count(),
                Revenue = g.Sum(oi => oi.Quantity * oi.UnitPrice),
                AveragePrice = g.Average(oi => oi.UnitPrice),
                UniqueBuyers = g.Select(oi => oi.Order!.UserId).Distinct().Count()
            })
            .FirstOrDefaultAsync();

        // Previous period for comparison
        var previousSales = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.Product!.Id == productId && 
                        oi.Order!.OrderDate >= sixtyDaysAgo && 
                        oi.Order!.OrderDate < thirtyDaysAgo)
            .GroupBy(oi => 1)
            .Select(g => new { Count = g.Count() })
            .FirstOrDefaultAsync();

        // Views and searches
        var views = await _db.UserBehaviorAnalytics
            .AsNoTracking()
            .Where(u => u.ProductId == productId && u.ActionDateTime >= thirtyDaysAgo && u.ActionType == "ProductView")
            .CountAsync();

        var searches = await _db.UserBehaviorAnalytics
            .AsNoTracking()
            .Where(u => u.ProductId == productId && u.ActionDateTime >= thirtyDaysAgo && u.ActionType == "ProductSearch")
            .CountAsync();

        // Rating data
        var reviews = await _db.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == "Approved")
            .Select(r => r.Rating)
            .ToListAsync();

        var avgRating = reviews.Count > 0 ? (decimal)reviews.Average() : 0m;

        // Growth calculation
        var salesGrowth = 0m;
        if (previousSales?.Count > 0 && currentSales != null)
        {
            salesGrowth = ((decimal)(currentSales.Count - previousSales.Count) / previousSales.Count) * 100;
        }

        // Trend determination
        var trend = salesGrowth switch
        {
            > 20 => "Up",
            < -20 => "Down",
            _ => "Stable"
        };

        var conversionRate = views > 0 ? (decimal)(currentSales?.Count ?? 0) / views * 100 : 0;
        var ctr = (views + searches) > 0 ? (decimal)views / (views + searches + 1) * 100 : 0;

        var metric = new SalesMetricsSnapshot
        {
            ProductId = productId,
            TotalSales = currentSales?.Count ?? 0,
            TotalRevenue = currentSales?.Revenue ?? 0,
            AverageSellingPrice = currentSales?.AveragePrice ?? 0,
            UniqueBuyers = currentSales?.UniqueBuyers ?? 0,
            PageViews = views,
            SearchImpressions = searches,
            ClickThroughRate = ctr,
            ConversionRate = conversionRate,
            AverageRating = avgRating,
            ReviewCount = reviews.Count,
            ReturnRate = 0, // Calculate from returns data
            PeriodStartDate = thirtyDaysAgo,
            PeriodEndDate = DateTime.UtcNow,
            SalesTrend = trend,
            SnapshotDateTime = DateTime.UtcNow
        };

        _db.SalesMetricsSnapshots.Add(metric);
        await _db.SaveChangesAsync();

        _logger.LogDebug($"Sales metrics updated for product {productId}");
    }

    /// <summary>
    /// Gets the latest metrics snapshot for a product
    /// </summary>
    public async Task<SalesMetricsSnapshot?> GetLatestMetricsAsync(int productId)
    {
        return await _db.SalesMetricsSnapshots
            .AsNoTracking()
            .Where(s => s.ProductId == productId)
            .OrderByDescending(s => s.SnapshotDateTime)
            .FirstOrDefaultAsync();
    }

    /// <summary>
    /// Refreshes metrics for all products
    /// </summary>
    public async Task RefreshAllMetricsAsync()
    {
        _logger.LogInformation("Starting bulk metrics refresh");

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => p.Id)
            .ToListAsync();

        foreach (var productId in products)
        {
            await UpdateSalesMetricsAsync(productId);
        }

        _logger.LogInformation($"Metrics refreshed for {products.Count} products");
    }

    // ==================== BULK OPERATIONS ====================

    /// <summary>
    /// Runs daily intelligent analysis
    /// Updates metrics and trending products
    /// </summary>
    public async Task RunDailyAnalysisAsync()
    {
        _logger.LogInformation("Starting daily intelligent analysis");
        try
        {
            await RefreshAllMetricsAsync();
            await RefreshTrendingProductsAsync();
            _logger.LogInformation("Daily analysis completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during daily analysis");
            throw;
        }
    }

    /// <summary>
    /// Runs weekly intelligent analysis
    /// Performs comprehensive analysis and flash sale suggestions
    /// </summary>
    public async Task RunWeeklyAnalysisAsync()
    {
        _logger.LogInformation("Starting weekly intelligent analysis");
        try
        {
            await RunDailyAnalysisAsync();
            await RefreshFlashSaleSuggestionsAsync();
            await AnalyzeTrendingProductsAsync(12, "Last7Days");
            _logger.LogInformation("Weekly analysis completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during weekly analysis");
            throw;
        }
    }

    // ==================== INTERNAL HELPERS ====================

    /// <summary>
    /// Analyzes a single product for trending status
    /// Returns: (confidence score, metrics)
    /// </summary>
    private async Task<(decimal score, (int SalesCount, int ViewCount, int WishlistCount, decimal AverageRating, decimal SalesGrowthRate) metrics)> AnalyzeProductTrendingAsync(int productId, DateTime startDate)
    {
        // Sales count
        var salesCount = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.ProductId == productId && oi.Order!.OrderDate >= startDate)
            .CountAsync();

        // View count
        var viewCount = await _db.UserBehaviorAnalytics
            .AsNoTracking()
            .Where(u => u.ProductId == productId && u.ActionDateTime >= startDate && u.ActionType == "ProductView")
            .CountAsync();

        // Wishlist adds
        var wishlistCount = await _db.UserBehaviorAnalytics
            .AsNoTracking()
            .Where(u => u.ProductId == productId && u.ActionDateTime >= startDate && u.ActionType == "AddToWishlist")
            .CountAsync();

        // Average rating
        var reviews = await _db.ProductReviews
            .AsNoTracking()
            .Where(r => r.ProductId == productId && r.Status == "Approved")
            .Select(r => r.Rating)
            .ToListAsync();

        var avgRating = reviews.Count > 0 ? (decimal)reviews.Average() : 0;

        // Sales growth (compare to previous period)
        var previousStartDate = startDate.AddDays(-30);
        var previousSalesCount = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.ProductId == productId && oi.Order!.OrderDate >= previousStartDate && oi.Order!.OrderDate < startDate)
            .CountAsync();

        var salesGrowth = previousSalesCount > 0 ? ((salesCount - previousSalesCount) / (decimal)previousSalesCount) * 100 : 0;

        // Calculate confidence score (weighted formula)
        // Sales weight: 40%, Views weight: 30%, Wishlist weight: 20%, Rating weight: 10%
        var score = (salesCount * 0.4m) + (viewCount * 0.3m * 0.1m) + (wishlistCount * 0.2m) + (avgRating * 10 * 0.1m);

        // Boost score if growth is positive
        if (salesGrowth > 0)
            score *= (1 + (salesGrowth / 100) * 0.5m);

        return (Math.Min(score, 100), (salesCount, viewCount, wishlistCount, avgRating, salesGrowth));
    }

    /// <summary>
    /// Analyzes a single product for flash sale opportunity
    /// </summary>
    private async Task<FlashSaleProductSuggestion?> AnalyzeFlashSaleOpportunityAsync(int productId)
    {
        var product = await _db.Products
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null)
            return null;

        // Get inventory level (sum of variants)
        var inventory = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == productId)
            .SumAsync(v => v.Stock);

        // Get recent sales
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var recentSalesCount = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.ProductId == productId && oi.Order!.OrderDate >= thirtyDaysAgo)
            .CountAsync();

        // Calculate metrics
        var dailySalesAverage = recentSalesCount / 30m;
        var daysOfInventory = dailySalesAverage > 0 ? inventory / dailySalesAverage : 0;

        // Suggest discount based on inventory level
        decimal suggestedDiscount = daysOfInventory switch
        {
            > 60 => 25, // High inventory
            > 30 => 15, // Medium inventory
            > 15 => 10, // Normal inventory
            _ => 0      // Low inventory - no need for flash sale
        };

        // If suggested discount is 0, skip this product
        if (suggestedDiscount == 0)
            return null;

        var suggestedFlashPrice = product.BasePrice * (1 - (suggestedDiscount / 100));
        var estimatedBoost = suggestedDiscount switch
        {
            >= 25 => 150,
            >= 15 => 100,
            >= 10 => 50,
            _ => 0
        };

        var estimatedRevenueLift = (estimatedBoost / 100m) * dailySalesAverage * suggestedFlashPrice;

        var suggestion = new FlashSaleProductSuggestion
        {
            ProductId = productId,
            SuggestedDiscountPercentage = suggestedDiscount,
            SuggestedFlashPrice = suggestedFlashPrice,
            SuggestionReason = daysOfInventory > 60 ? "High inventory clearance" :
                             daysOfInventory > 30 ? "Inventory optimization" :
                             "Strategic promotion",
            CurrentInventory = (int)inventory,
            RecommendedQuantityForFlash = Math.Max(1, (int)(dailySalesAverage * 7)), // 7 days worth
            ExpectedSalesBoost = estimatedBoost,
            EstimatedRevenueLift = estimatedRevenueLift,
            CalculatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsActive = true,
            PriorityScore = suggestedDiscount + (estimatedBoost / 100m)
        };

        return suggestion;
    }

    /// <summary>
    /// Gets period dates for analysis
    /// </summary>
    private (DateTime startDate, string periodName) GetPeriodDates(string period) => period switch
    {
        "Last7Days" => (DateTime.UtcNow.AddDays(-7), "Last7Days"),
        "Last30Days" => (DateTime.UtcNow.AddDays(-30), "Last30Days"),
        "Last90Days" => (DateTime.UtcNow.AddDays(-90), "Last90Days"),
        _ => (DateTime.UtcNow.AddDays(-30), "Last30Days")
    };

    /// <summary>
    /// Gets expiration days for suggestions based on period
    /// </summary>
    private int GetExpirationDays(string period) => period switch
    {
        "Last7Days" => 3,
        "Last30Days" => 7,
        "Last90Days" => 14,
        _ => 7
    };
}
