using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Intelligence;

namespace Sparkle.Infrastructure.Intelligence;

/// <summary>
/// ML-powered customer intelligence service
/// Provides user profiling, segmentation, and predictive analytics
/// </summary>
public class CustomerIntelligence : ICustomerIntelligence
{
    private readonly ApplicationDbContext _db;

    public CustomerIntelligence(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<UserBehaviorProfile> GetUserProfileAsync(string userId)
    {
        var profile = new UserBehaviorProfile { UserId = userId };

        // Get purchase history
        var orders = await _db.Orders
            .Include(o => o.Items)
            .ThenInclude(oi => oi.Product)
            .Where(o => o.UserId == userId)
            .ToListAsync();

        if (!orders.Any())
        {
            return profile;
        }

        // Calculate category affinity scores
        var categoryPurchases = orders
            .SelectMany(o => o.Items)
            .Where(oi => oi.Product != null)
            .GroupBy(oi => oi.Product.CategoryId)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        var maxCatCount = categoryPurchases.Values.Max();
        profile.CategoryAffinityScores = categoryPurchases
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value / maxCatCount);

        // Calculate brand affinity
        var brandPurchases = orders
            .SelectMany(o => o.Items)
            .Where(oi => oi.Product?.BrandId != null)
            .GroupBy(oi => oi.Product.BrandId!.Value)
            .ToDictionary(g => g.Key, g => (double)g.Count());

        if (brandPurchases.Any())
        {
            var maxBrandCount = brandPurchases.Values.Max();
            profile.BrandAffinityScores = brandPurchases
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value / maxBrandCount);
        }

        // Price preferences
        var orderValues = orders.Select(o => o.TotalAmount).ToList();
        profile.PreferredPriceRangeLow = orderValues.Min();
        profile.PreferredPriceRangeHigh = orderValues.Max();
        
        var avgOrderValue = (decimal)orderValues.Average();
        profile.PriceSensitivityScore = avgOrderValue < 1000 ? 0.8 : 
                                        avgOrderValue < 5000 ? 0.5 : 0.2;

        // Shopping patterns
        var orderDates = orders.Select(o => o.OrderDate).OrderBy(d => d).ToList();
        if (orderDates.Count >= 2)
        {
            var avgDaysBetweenOrders = orderDates
                .Zip(orderDates.Skip(1), (a, b) => (b - a).TotalDays)
                .Average();
            
            profile.ImpulseBuyerScore = avgDaysBetweenOrders < 7 ? 0.8 : 
                                        avgDaysBetweenOrders < 30 ? 0.4 : 0.1;
        }

        // Loyalty score based on order count and recency
        var lastOrderDaysAgo = (DateTime.UtcNow - orderDates.LastOrDefault()).TotalDays;
        profile.LoyaltyScore = Math.Min(1.0, orders.Count / 10.0) * 
                              Math.Max(0, 1 - lastOrderDaysAgo / 180);

        // Churn risk
        profile.ChurnRiskScore = await CalculateChurnRiskAsync(userId);

        // Cart abandonment rate
        var carts = await _db.Carts.Where(c => c.UserId == userId).ToListAsync();
        var completedOrders = orders.Count;
        if (carts.Any())
        {
            profile.CartAbandonmentRate = 1 - (completedOrders / (double)(completedOrders + carts.Count));
        }

        // Conversion rate
        var totalVisits = await _db.StoreVisits.CountAsync(sv => sv.UserId == userId);
        if (totalVisits > 0)
        {
            profile.ConversionRate = completedOrders / (double)totalVisits;
        }

        // Inferred interests from search history
        var recentSearches = await _db.SearchAnalytics
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SearchedAt)
            .Take(20)
            .Select(s => s.SearchQuery)
            .ToListAsync();
        
        profile.InferredInterests = recentSearches.Distinct().Take(10).ToList();

        profile.LastUpdated = DateTime.UtcNow;
        return profile;
    }

    public async Task<List<CustomerSegment>> GetAllSegmentsAsync()
    {
        // Define segments based on behavioral patterns
        var segments = new List<CustomerSegment>();

        // High Value Customers
        var highValueUsers = await _db.Orders
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, TotalSpent = g.Sum(o => o.TotalAmount) })
            .Where(x => x.TotalSpent > 50000)
            .Select(x => x.UserId)
            .ToListAsync();

        segments.Add(new CustomerSegment
        {
            SegmentId = "high_value",
            Name = "High Value Customers",
            Description = "Customers with total purchases over ৳50,000",
            UserIds = highValueUsers,
            UserCount = highValueUsers.Count,
            LifetimeValueScore = 0.9,
            RecommendedCampaigns = new List<string> { "Exclusive Offers", "VIP Discounts", "Priority Access" }
        });

        // Frequent Buyers
        var frequentBuyers = await _db.Orders
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, OrderCount = g.Count() })
            .Where(x => x.OrderCount >= 5)
            .Select(x => x.UserId)
            .ToListAsync();

        segments.Add(new CustomerSegment
        {
            SegmentId = "frequent_buyers",
            Name = "Frequent Buyers",
            Description = "Customers with 5+ orders",
            UserIds = frequentBuyers,
            UserCount = frequentBuyers.Count,
            LifetimeValueScore = 0.7,
            RecommendedCampaigns = new List<string> { "Loyalty Points Bonus", "Free Shipping" }
        });

        // At Risk (Churn)
        var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
        var ninetyDaysAgo = DateTime.UtcNow.AddDays(-90);
        var atRiskUsers = await _db.Orders
            .GroupBy(o => o.UserId)
            .Select(g => new { UserId = g.Key, LastOrder = g.Max(o => o.OrderDate) })
            .Where(x => x.LastOrder < ninetyDaysAgo)
            .Select(x => x.UserId)
            .ToListAsync();

        segments.Add(new CustomerSegment
        {
            SegmentId = "at_risk",
            Name = "At Risk (Churn)",
            Description = "Haven't purchased in 90+ days",
            UserIds = atRiskUsers,
            UserCount = atRiskUsers.Count,
            LifetimeValueScore = 0.3,
            RecommendedCampaigns = new List<string> { "Re-engagement Offer", "We Miss You Discount" }
        });

        // New Customers
        var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
        var newUsers = await _db.Users
            .Where(u => u.RegisteredAt >= sevenDaysAgo)
            .Select(u => u.Id)
            .ToListAsync();

        segments.Add(new CustomerSegment
        {
            SegmentId = "new_customers",
            Name = "New Customers",
            Description = "Registered in the last 7 days",
            UserIds = newUsers,
            UserCount = newUsers.Count,
            LifetimeValueScore = 0.5,
            RecommendedCampaigns = new List<string> { "Welcome Discount", "First Purchase Offer" }
        });

        // Bargain Hunters
        var bargainHunters = await _db.VoucherUsages
            .GroupBy(v => v.UserId)
            .Select(g => new { UserId = g.Key, VoucherCount = g.Count() })
            .Where(x => x.VoucherCount >= 3)
            .Select(x => x.UserId)
            .ToListAsync();

        segments.Add(new CustomerSegment
        {
            SegmentId = "bargain_hunters",
            Name = "Bargain Hunters",
            Description = "Used 3+ discount vouchers",
            UserIds = bargainHunters,
            UserCount = bargainHunters.Count,
            Characteristics = new Dictionary<string, double> { { "PriceSensitivity", 0.9 } },
            RecommendedCampaigns = new List<string> { "Flash Sale Alerts", "Clearance Events" }
        });

        return segments;
    }

    public async Task<CustomerSegment?> GetUserSegmentAsync(string userId)
    {
        var segments = await GetAllSegmentsAsync();
        return segments.FirstOrDefault(s => s.UserIds.Contains(userId));
    }

    public async Task<double> CalculateChurnRiskAsync(string userId)
    {
        var lastOrder = await _db.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .FirstOrDefaultAsync();

        if (lastOrder == null) return 0.5; // New user

        var daysSinceLastOrder = (DateTime.UtcNow - lastOrder.OrderDate).TotalDays;

        // Simple churn risk model
        // 0-30 days: Low risk
        // 30-60 days: Medium risk
        // 60-90 days: High risk
        // 90+ days: Very high risk
        double churnRisk = daysSinceLastOrder switch
        {
            < 30 => 0.1,
            < 60 => 0.3,
            < 90 => 0.6,
            < 180 => 0.8,
            _ => 0.95
        };

        // Adjust for order frequency
        var orderCount = await _db.Orders.CountAsync(o => o.UserId == userId);
        if (orderCount > 10) churnRisk *= 0.7; // Loyal customers less likely to churn
        else if (orderCount < 3) churnRisk *= 1.2; // New/infrequent customers more likely

        return Math.Min(1.0, churnRisk);
    }

    public async Task<double> CalculateLifetimeValueAsync(string userId)
    {
        var orders = await _db.Orders
            .Where(o => o.UserId == userId)
            .ToListAsync();

        if (!orders.Any()) return 0;

        var totalSpent = (double)orders.Sum(o => o.TotalAmount);
        var orderCount = orders.Count;
        var accountAge = (DateTime.UtcNow - orders.Min(o => o.OrderDate)).TotalDays;
        
        if (accountAge < 1) accountAge = 1;

        // Simple LTV calculation: (Avg Order Value * Order Frequency * Expected Lifetime)
        var avgOrderValue = totalSpent / orderCount;
        var orderFrequency = orderCount / (accountAge / 30.0); // Orders per month
        var expectedLifetimeMonths = 24; // Assumed 2 year customer lifetime

        var ltv = avgOrderValue * orderFrequency * expectedLifetimeMonths;
        
        return ltv;
    }

    public async Task RefreshSegmentationAsync()
    {
        // Trigger segment recalculation
        await GetAllSegmentsAsync();
    }
}
