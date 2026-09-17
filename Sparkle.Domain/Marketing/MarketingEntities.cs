using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Sellers;
using Sparkle.Domain.Orders;

namespace Sparkle.Domain.Marketing;

public class Coupon : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    // Discount Details
    public string DiscountType { get; set; } = "Percentage"; // Percentage, FixedAmount
    public decimal DiscountValue { get; set; }
    public decimal? MaxDiscountAmount { get; set; }
    public decimal MinimumPurchaseAmount { get; set; }
    
    // Usage Limits
    public int? MaxUsageTotal { get; set; }
    public int? MaxUsagePerUser { get; set; }
    public int CurrentUsageCount { get; set; }
    
    // Validity
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Restrictions
    public string? ApplicableCategories { get; set; } // JSON array of category IDs
    public string? ExcludedCategories { get; set; } // JSON array
    public string? ApplicableProducts { get; set; } // JSON array of product IDs
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    
    public bool IsPublic { get; set; } = true; // false = targeted/private
    public string? TargetUserSegment { get; set; } // NewUsers, PremiumMembers, etc.
    
    public ICollection<VoucherUsage> UsageHistory { get; set; } = new List<VoucherUsage>();
}

public class VoucherUsage : BaseEntity
{
    public int CouponId { get; set; }
    public Coupon Coupon { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public decimal DiscountAmount { get; set; }
    public DateTime UsedAt { get; set; } = DateTime.UtcNow;
}

public class FlashDeal : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? BannerImage { get; set; }
    
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Display
    public int DisplayOrder { get; set; }
    public bool ShowOnHomepage { get; set; } = true;
    public string? BadgeText { get; set; } // "Limited Time", "Hurry Up", etc.
    
    public ICollection<FlashDealProduct> Products { get; set; } = new List<FlashDealProduct>();
}

public class FlashDealProduct : BaseEntity
{
    public int FlashDealId { get; set; }
    public FlashDeal FlashDeal { get; set; } = null!;
    
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    public decimal OriginalPrice { get; set; }
    public decimal DealPrice { get; set; }
    public decimal DiscountPercentage { get; set; }
    
    public int? StockLimit { get; set; }
    public int SoldCount { get; set; }
    
    public int DisplayOrder { get; set; }
}

public class Campaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public string CampaignType { get; set; } = string.Empty; // SeasonalSale, BrandDay, CategorySale, MegaSale
    
    // Images
    public string? BannerImageDesktop { get; set; }
    public string? BannerImageMobile { get; set; }
    public string? ThumbnailImage { get; set; }
    
    // Schedule
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsFeatured { get; set; }
    
    // SEO
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    
    // Display
    public int DisplayOrder { get; set; }
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    
    // Analytics
    public int ViewCount { get; set; }
    public int ClickCount { get; set; }
    public int OrderCount { get; set; }
    public decimal TotalRevenue { get; set; }
    
    public ICollection<CampaignProduct> Products { get; set; } = new List<CampaignProduct>();
    public ICollection<CampaignCategory> Categories { get; set; } = new List<CampaignCategory>();
}

public class CampaignProduct : BaseEntity
{
    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    public decimal? SpecialPrice { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsFeatured { get; set; }
}

public class CampaignCategory : BaseEntity
{
    public int CampaignId { get; set; }
    public Campaign Campaign { get; set; } = null!;
    
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    
    public decimal? DiscountPercentage { get; set; }
    public int DisplayOrder { get; set; }
}

public class Banner : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? SubTitle { get; set; }
    public string? Description { get; set; }
    
    public string BannerType { get; set; } = "Hero"; // Hero, Sidebar, TopStrip, CategoryBanner, ProductBanner
    public string Position { get; set; } = "Homepage"; // Homepage, CategoryPage, ProductPage, CheckoutPage
    
    // Images
    public string? ImageUrlDesktop { get; set; } = string.Empty;
    public string? ImageUrlMobile { get; set; }
    public string? ImageUrlTablet { get; set; }
    
    // Link
    public string? LinkUrl { get; set; }
    public string LinkTarget { get; set; } = "_self"; // _self, _blank
    public string? ButtonText { get; set; }
    
    // Schedule
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Display
    public int DisplayOrder { get; set; }
    public string? BackgroundColor { get; set; }
    public string? TextColor { get; set; }
    
    // Analytics
    public int ViewCount { get; set; }
    public int ClickCount { get; set; }
}

public class EmailSubscription : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    public string SubscriptionType { get; set; } = "Newsletter"; // Newsletter, Promotions, ProductUpdates, All
    public string Status { get; set; } = "Active"; // Active, Unsubscribed, Bounced
    
    public string? Source { get; set; } // Homepage, Checkout, Account, etc.
    public string? ReferrerUrl { get; set; }
    
    public DateTime SubscribedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UnsubscribedAt { get; set; }
    public string? UnsubscribeReason { get; set; }
    
    public string? PreferredLanguage { get; set; }
    public string? InterestCategories { get; set; } // JSON array
    
    // Stats
    public int EmailsSent { get; set; }
    public int EmailsOpened { get; set; }
    public int LinksClicked { get; set; }
    public DateTime? LastEmailSentAt { get; set; }
    public DateTime? LastEmailOpenedAt { get; set; }
}

public class NewsletterCampaign : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string? PreviewText { get; set; }
    
    public string HtmlContent { get; set; } = string.Empty;
    public string? TextContent { get; set; }
    
    public string Status { get; set; } = "Draft"; // Draft, Scheduled, Sending, Sent, Cancelled
    
    public DateTime? ScheduledAt { get; set; }
    public DateTime? SentAt { get; set; }
    
    // Targeting
    public string? TargetSegment { get; set; } // AllSubscribers, NewUsers, ActiveCustomers, etc.
    public string? TargetCategories { get; set; } // JSON array
    
    // Stats
    public int TotalRecipients { get; set; }
    public int SentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int OpenedCount { get; set; }
    public int ClickedCount { get; set; }
    public int UnsubscribedCount { get; set; }
    public int BouncedCount { get; set; }
}
