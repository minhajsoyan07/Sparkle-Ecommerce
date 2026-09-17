using Sparkle.Domain.Common;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Identity;

namespace Sparkle.Domain.Content;

/// <summary>
/// Defines a homepage section that displays products (e.g., "Shop by Category", "Trending", "Flash Sale")
/// </summary>
public class HomepageSection : BaseEntity
{
    /// <summary>
    /// Section name (Shop by Category, Trending Products, Flash Sale, etc.)
    /// </summary>
    public string? Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique slug for internal reference
    /// </summary>
    public string? Slug { get; set; } = string.Empty;

    /// <summary>
    /// Display title shown to users
    /// </summary>
    public string? DisplayTitle { get; set; } = string.Empty;

    /// <summary>
    /// Section description/subtitle
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Type of section: CategoryShop, Trending, FlashSale, Recommended, etc.
    /// </summary>
    public string? SectionType { get; set; } = string.Empty; // CategoryShop, TrendingProducts, FlashSale, RecommendedProducts

    /// <summary>
    /// Background color or image URL for section styling
    /// </summary>
    public string? BackgroundColor { get; set; }

    /// <summary>
    /// Banner image URL for the section
    /// </summary>
    public string? BannerImageUrl { get; set; }

    /// <summary>
    /// Display order on homepage (lower number = appears first)
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Is this section currently visible on homepage?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Maximum number of products to display in this section
    /// </summary>
    public int MaxProductsToDisplay { get; set; } = 12;

    /// <summary>
    /// Number of products per row (1, 2, 3, 4, 5, 6)
    /// </summary>
    public int ProductsPerRow { get; set; } = 4;

    /// <summary>
    /// Layout type: Grid, Carousel, List, etc.
    /// </summary>
    public string LayoutType { get; set; } = "Grid"; // Grid, Carousel, List

    /// <summary>
    /// Product card size: Small, Medium, Large
    /// </summary>
    public string CardSize { get; set; } = "Medium"; // Small, Medium, Large

    /// <summary>
    /// Should use automated/intelligent selection?
    /// </summary>
    public bool UseAutomatedSelection { get; set; } = true;

    /// <summary>
    /// Is manual selection currently overriding automated selection?
    /// </summary>
    public bool UseManualSelection { get; set; } = false;

    /// <summary>
    /// Show product rating?
    /// </summary>
    public bool ShowRating { get; set; } = true;

    /// <summary>
    /// Show product price?
    /// </summary>
    public bool ShowPrice { get; set; } = true;

    /// <summary>
    /// Show discount/save amount?
    /// </summary>
    public bool ShowDiscount { get; set; } = true;

    /// <summary>
    /// Last time automated selection was run
    /// </summary>
    public DateTime? LastAutomationRunTime { get; set; }

    /// <summary>
    /// Admin user who created this section
    /// </summary>
    public string? CreatedByUserId { get; set; }
    public ApplicationUser? CreatedByUser { get; set; }

    /// <summary>
    /// Manually selected products for this section
    /// </summary>
    public ICollection<HomepageSectionProduct> ManualProducts { get; set; } = new List<HomepageSectionProduct>();

    /// <summary>
    /// Related categories (for category shop sections)
    /// </summary>
    public ICollection<HomepageSectionCategory> RelatedCategories { get; set; } = new List<HomepageSectionCategory>();

    /// <summary>
    /// Audit log for section changes
    /// </summary>
    public ICollection<HomepageSectionAuditLog> AuditLogs { get; set; } = new List<HomepageSectionAuditLog>();
}

/// <summary>
/// Represents a product manually added to a homepage section
/// </summary>
public class HomepageSectionProduct : BaseEntity
{
    /// <summary>
    /// Homepage section this product belongs to
    /// </summary>
    public int SectionId { get; set; }
    public HomepageSection Section { get; set; } = null!;

    /// <summary>
    /// The product to display
    /// </summary>
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>
    /// Display order within the section
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Optional promotional text for this product in this section
    /// </summary>
    public string? PromotionalText { get; set; }

    /// <summary>
    /// Optional special badge/label for this product
    /// </summary>
    public string? BadgeText { get; set; } // "New", "Best Seller", "Limited", etc.

    /// <summary>
    /// Is this product currently active in this section?
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Associates categories with a "Shop by Category" section
/// </summary>
public class HomepageSectionCategory : BaseEntity
{
    /// <summary>
    /// Homepage section
    /// </summary>
    public int SectionId { get; set; }
    public HomepageSection Section { get; set; } = null!;

    /// <summary>
    /// Category to display in this section
    /// </summary>
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    /// <summary>
    /// Display order of categories within the section
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Custom display title for category in this section
    /// </summary>
    public string? CustomDisplayTitle { get; set; }

    /// <summary>
    /// Number of products to show from this category
    /// </summary>
    public int ProductCountToShow { get; set; } = 6;

    /// <summary>
    /// Is this category active in this section?
    /// </summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Stores intelligent recommendations for trending products
/// Generated by AI/ML algorithms analyzing user behavior and sales patterns
/// </summary>
public class TrendingProductSuggestion : BaseEntity
{
    /// <summary>
    /// Product being suggested as trending
    /// </summary>
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>
    /// Confidence score (0-100) indicating how strongly this product is trending
    /// Based on sales velocity, search volume, wishlist adds, etc.
    /// </summary>
    public decimal ConfidenceScore { get; set; }

    /// <summary>
    /// Total sales in the period analyzed
    /// </summary>
    public int SalesCount { get; set; }

    /// <summary>
    /// Number of views/searches for this product
    /// </summary>
    public int ViewCount { get; set; }

    /// <summary>
    /// Number of times added to wishlist
    /// </summary>
    public int WishlistCount { get; set; }

    /// <summary>
    /// Average rating during the period
    /// </summary>
    public decimal AverageRating { get; set; }

    /// <summary>
    /// Growth rate of sales compared to previous period (percentage)
    /// </summary>
    public decimal SalesGrowthRate { get; set; }

    /// <summary>
    /// When this suggestion was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this suggestion expires and needs recalculation
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Is this suggestion currently valid?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Analysis period (e.g., "Last7Days", "Last30Days", "Last90Days")
    /// </summary>
    public string AnalysisPeriod { get; set; } = "Last30Days";

    /// <summary>
    /// Rank among trending products (1 = most trending)
    /// </summary>
    public int Rank { get; set; }
}

/// <summary>
/// Stores intelligent suggestions for flash sale products
/// Generated by analyzing inventory levels, sales patterns, and strategic pricing
/// </summary>
public class FlashSaleProductSuggestion : BaseEntity
{
    /// <summary>
    /// Product suggested for flash sale
    /// </summary>
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>
    /// Suggested discount percentage
    /// </summary>
    public decimal SuggestedDiscountPercentage { get; set; }

    /// <summary>
    /// Suggested flash sale price
    /// </summary>
    public decimal SuggestedFlashPrice { get; set; }

    /// <summary>
    /// Reason for suggestion (high inventory, slow sales, seasonal, strategic, etc.)
    /// </summary>
    public string SuggestionReason { get; set; } = string.Empty;

    /// <summary>
    /// Current inventory level
    /// </summary>
    public int CurrentInventory { get; set; }

    /// <summary>
    /// Recommended inventory to clear with flash sale
    /// </summary>
    public int RecommendedQuantityForFlash { get; set; }

    /// <summary>
    /// Expected sales boost percentage with this discount
    /// </summary>
    public decimal ExpectedSalesBoost { get; set; }

    /// <summary>
    /// Estimated revenue impact
    /// </summary>
    public decimal EstimatedRevenueLift { get; set; }

    /// <summary>
    /// When suggestion was calculated
    /// </summary>
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When suggestion expires
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Is this suggestion currently valid?
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Priority score (higher = more recommended)
    /// </summary>
    public decimal PriorityScore { get; set; }
}

/// <summary>
/// Tracks user interaction patterns for intelligent analysis
/// Used by AI/ML to make better recommendations
/// </summary>
public class UserBehaviorAnalytic : BaseEntity
{
    /// <summary>
    /// User whose behavior is being tracked
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    /// <summary>
    /// Type of action: ProductView, ProductSearch, AddToCart, AddToWishlist, Purchase, etc.
    /// </summary>
    public string ActionType { get; set; } = string.Empty;

    /// <summary>
    /// Product involved in the action (if applicable)
    /// </summary>
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    /// <summary>
    /// Category involved (if applicable)
    /// </summary>
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }

    /// <summary>
    /// Search term (if this was a search action)
    /// </summary>
    public string? SearchTerm { get; set; }

    /// <summary>
    /// Time spent on product/page (in seconds)
    /// </summary>
    public int? TimeSpentSeconds { get; set; }

    /// <summary>
    /// Device type used
    /// </summary>
    public string? DeviceType { get; set; }

    /// <summary>
    /// Session ID for grouping actions
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Timestamp of the action
    /// </summary>
    public DateTime ActionDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// IP address for geo-tracking
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Browser/user agent
    /// </summary>
    public string? UserAgent { get; set; }
}

/// <summary>
/// Aggregated sales metrics used for intelligent analysis
/// Refreshed periodically to feed ML algorithms
/// </summary>
public class SalesMetricsSnapshot : BaseEntity
{
    /// <summary>
    /// Product these metrics are for
    /// </summary>
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;

    /// <summary>
    /// Total sales count in the period
    /// </summary>
    public int TotalSales { get; set; }

    /// <summary>
    /// Total revenue from sales
    /// </summary>
    public decimal TotalRevenue { get; set; }

    /// <summary>
    /// Average selling price
    /// </summary>
    public decimal AverageSellingPrice { get; set; }

    /// <summary>
    /// Number of unique customers
    /// </summary>
    public int UniqueBuyers { get; set; }

    /// <summary>
    /// Product page views
    /// </summary>
    public int PageViews { get; set; }

    /// <summary>
    /// Search impressions
    /// </summary>
    public int SearchImpressions { get; set; }

    /// <summary>
    /// Click-through rate
    /// </summary>
    public decimal ClickThroughRate { get; set; }

    /// <summary>
    /// Conversion rate
    /// </summary>
    public decimal ConversionRate { get; set; }

    /// <summary>
    /// Average rating
    /// </summary>
    public decimal AverageRating { get; set; }

    /// <summary>
    /// Number of reviews
    /// </summary>
    public int ReviewCount { get; set; }

    /// <summary>
    /// Return rate
    /// </summary>
    public decimal ReturnRate { get; set; }

    /// <summary>
    /// Period start date
    /// </summary>
    public DateTime PeriodStartDate { get; set; }

    /// <summary>
    /// Period end date
    /// </summary>
    public DateTime PeriodEndDate { get; set; }

    /// <summary>
    /// When this snapshot was created
    /// </summary>
    public DateTime SnapshotDateTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Trend indicator: Up, Down, Stable
    /// </summary>
    public string SalesTrend { get; set; } = "Stable";
}

/// <summary>
/// Audit log for homepage section changes
/// Tracks all manual selections and configuration changes
/// </summary>
public class HomepageSectionAuditLog : BaseEntity
{
    /// <summary>
    /// Homepage section being modified
    /// </summary>
    public int SectionId { get; set; }
    public HomepageSection Section { get; set; } = null!;

    /// <summary>
    /// Type of change: Created, Updated, ProductAdded, ProductRemoved, ProductReordered, etc.
    /// </summary>
    public string ChangeType { get; set; } = string.Empty;

    /// <summary>
    /// What was changed
    /// </summary>
    public string PropertyName { get; set; } = string.Empty;

    /// <summary>
    /// Old value (for updates)
    /// </summary>
    public string? OldValue { get; set; }

    /// <summary>
    /// New value (for updates)
    /// </summary>
    public string? NewValue { get; set; }

    /// <summary>
    /// Admin who made the change
    /// </summary>
    public string? ChangedByUserId { get; set; }
    public ApplicationUser? ChangedByUser { get; set; }

    /// <summary>
    /// When the change was made
    /// </summary>
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Additional notes about the change
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Was this change automated (by AI) or manual?
    /// </summary>
    public bool IsAutomatedChange { get; set; } = false;
}
