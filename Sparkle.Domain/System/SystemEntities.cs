using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Sellers;

namespace Sparkle.Domain.System;

// ==================== SUPPORT & COMMUNICATION ====================

public class SupportTicket : BaseEntity
{
    public string TicketNumber { get; set; } = string.Empty;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    
    public string Category { get; set; } = string.Empty; // Order, Product, Payment, Refund, Technical, Account, Other
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
    public string Status { get; set; } = "Open"; // Open, InProgress, Waiting, Resolved, Closed
    
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public string? AssignedTo { get; set; }
    public DateTime? AssignedAt { get; set; }
    
    // CreatedAt inherited from BaseEntity
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
    
    public string? ResolutionNotes { get; set; }
    public int? SatisfactionRating { get; set; } // 1-5
    public string? CustomerFeedback { get; set; }
    
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
}

public class TicketMessage : BaseEntity
{
    public int SupportTicketId { get; set; }
    public SupportTicket SupportTicket { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string Message { get; set; } = string.Empty;
    public bool IsStaffReply { get; set; }
    public bool IsInternal { get; set; } // Internal notes not visible to customer
    
    public string? Attachments { get; set; } // JSON array of file URLs
    
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
}

public class Notification : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string Type { get; set; } = string.Empty; // Order, Product, Promotion, System, Review, Message
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    
    public string? ImageUrl { get; set; }
    public string? IconClass { get; set; }
    
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    
    // CreatedAt inherited from BaseEntity
    public DateTime? ExpiresAt { get; set; }
    
    public string? RelatedEntityType { get; set; } // Order, Product, etc.
    public string? RelatedEntityId { get; set; }
}

public class ActivityLog : BaseEntity
{
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    public string ActivityType { get; set; } = string.Empty; // Login, Logout, OrderPlaced, ProductViewed, etc.
    public string EntityType { get; set; } = string.Empty; // User, Product, Order, Vendor
    public string? EntityId { get; set; }
    
    public string Action { get; set; } = string.Empty; // Create, Update, Delete, View
    public string? Description { get; set; }
    public string? Details { get; set; } // JSON
    
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceInfo { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

// ==================== SHIPPING & LOGISTICS ====================

public class ShippingMethod : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }
    
    public string DeliveryType { get; set; } = "Standard"; // Standard, Express, SameDay, NextDay
    public string EstimatedDelivery { get; set; } = string.Empty; // "3-5 days"
    
    public decimal BaseRate { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal FreeShippingThreshold { get; set; }
    
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    
    public string? AvailableRegions { get; set; } // JSON array
}

public class CourierPartner : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Logo { get; set; }
    
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    
    public string? TrackingUrlTemplate { get; set; } // e.g., "https://tracking.com/track/{trackingNumber}"
    public string? ApiEndpoint { get; set; }
    public string? ApiKey { get; set; }
    
    public bool IsActive { get; set; } = true;
    public bool SupportsRealTimeTracking { get; set; }
    public bool SupportsCashOnDelivery { get; set; }
    
    public decimal AverageRating { get; set; }
    public int TotalDeliveries { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }
}

public class ShippingZone : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    
    public string ZoneType { get; set; } = "City"; // City, District, Division, Country
    
    public string? Division { get; set; }
    public string? Districts { get; set; } // JSON array
    public string? Cities { get; set; } // JSON array
    public string? PostalCodes { get; set; } // JSON array
    
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    
    public ICollection<ShippingRate> ShippingRates { get; set; } = new List<ShippingRate>();
}

public class ShippingRate : BaseEntity
{
    public int ShippingZoneId { get; set; }
    public ShippingZone ShippingZone { get; set; } = null!;
    
    public int ShippingMethodId { get; set; }
    public ShippingMethod ShippingMethod { get; set; } = null!;
    
    public decimal BaseRate { get; set; }
    public decimal RatePerKg { get; set; }
    public decimal? FreeShippingThreshold { get; set; }
    
    public decimal? MinWeight { get; set; }
    public decimal? MaxWeight { get; set; }
    
    public bool IsActive { get; set; } = true;
}

// ==================== ANALYTICS & REPORTS ====================

public class ProductView : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    public string? SessionId { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? DeviceType { get; set; } // Mobile, Tablet, Desktop
    
    public string? ReferrerUrl { get; set; }
    public string? SourceChannel { get; set; } // Direct, Search, Social, Email
    
    public int ViewDurationSeconds { get; set; }
    public bool ViewedImages { get; set; }
    public bool ViewedDescription { get; set; }
    public bool ViewedReviews { get; set; }
    public bool AddedToCart { get; set; }
    public bool AddedToWishlist { get; set; }
    
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
}

public class SearchAnalytics : BaseEntity
{
    public string SearchQuery { get; set; } = string.Empty;
    public string? NormalizedQuery { get; set; }
    
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    public string? SessionId { get; set; }
    public int ResultCount { get; set; }
    public string? AppliedFilters { get; set; } // JSON
    public string? SortBy { get; set; }
    
    public bool HasResults { get; set; }
    public int? ClickedPosition { get; set; }
    public int? ClickedProductId { get; set; }
    public bool ConvertedToPurchase { get; set; }
    
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    
    public string? DeviceType { get; set; }
    public string? Location { get; set; }
}

public class SalesReport : BaseEntity
{
    public DateTime ReportDate { get; set; }
    public string Period { get; set; } = "Daily"; // Daily, Weekly, Monthly, Yearly
    
    // Sales Metrics
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int PendingOrders { get; set; }
    
    public decimal TotalRevenue { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal TotalDiscount { get; set; }
    public decimal TotalShipping { get; set; }
    public decimal TotalTax { get; set; }
    public decimal TotalRefunds { get; set; }
    
    public decimal AverageOrderValue { get; set; }
    public int TotalItemsSold { get; set; }
    public int UniqueCustomers { get; set; }
    public int NewCustomers { get; set; }
    public int RepeatCustomers { get; set; }
    
    // Product Performance
    public int TotalProducts { get; set; }
    public int ProductsWithSales { get; set; }
    public int OutOfStockProducts { get; set; }
    
    // Payment Methods
    public decimal CashOnDeliveryAmount { get; set; }
    public decimal OnlinePaymentAmount { get; set; }
    public decimal MobileBankingAmount { get; set; }
    
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

public class SellerEarning : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;
    
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public decimal ItemTotal { get; set; }
    public decimal PlatformCommissionRate { get; set; }
    public decimal PlatformCommission { get; set; }
    public decimal PlatformFees { get; set; }
    public decimal NetEarning { get; set; }
    
    public string Status { get; set; } = "Pending"; // Pending, Available, PaidOut
    public DateTime? AvailableForPayoutAt { get; set; } // After order completion + hold period
    public DateTime? PaidOutAt { get; set; }
    public int? PayoutId { get; set; }
    
    public DateTime EarnedAt { get; set; } = DateTime.UtcNow;
}

public class PlatformMetric : BaseEntity
{
    public DateTime MetricDate { get; set; }
    public string Period { get; set; } = "Daily";
    
    // User Metrics
    public int TotalUsers { get; set; }
    public int NewUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int TotalSellers { get; set; }
    public int ActiveSellers { get; set; }
    public int NewSellers { get; set; }
    
    // Product Metrics
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int NewProducts { get; set; }
    
    // Order Metrics
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal PlatformRevenue { get; set; }
    public decimal VendorRevenue { get; set; }
    
    // Traffic Metrics
    public int TotalVisits { get; set; }
    public int UniqueVisitors { get; set; }
    public int PageViews { get; set; }
    public decimal AverageSessionDuration { get; set; }
    public decimal BounceRate { get; set; }
    public decimal ConversionRate { get; set; }
    
    // Engagement Metrics
    public int TotalReviews { get; set; }
    public int TotalQuestions { get; set; }
    public int TotalWishlistAdds { get; set; }
    public int TotalCartAdds { get; set; }
    
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
