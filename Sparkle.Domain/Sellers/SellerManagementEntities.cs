using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;

namespace Sparkle.Domain.Sellers;

public class SellerDocument : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    public string DocumentType { get; set; } = string.Empty; // TradeLicense, NID, TIN, BankStatement
    public string DocumentNumber { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public long FileSize { get; set; }
    public string? MimeType { get; set; }
    
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    
    public string VerificationStatus { get; set; } = "Pending"; // Pending, Verified, Rejected
    public string? VerificationNotes { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public string? VerifiedBy { get; set; }
    
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

public class SellerBankAccount : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    public string AccountType { get; set; } = string.Empty; // Bank, Bkash, Nagad, Rocket
    
    // For Bank Accounts
    public string? BankName { get; set; }
    public string? BranchName { get; set; }
    public string? AccountHolderName { get; set; }
    public string? AccountNumber { get; set; }
    public string? RoutingNumber { get; set; }
    public string? SwiftCode { get; set; }
    
    // For Mobile Banking
    public string? MobileNumber { get; set; }
    public string? AccountName { get; set; }
    
    public bool IsDefault { get; set; }
    public bool IsVerified { get; set; }
    public DateTime? VerifiedAt { get; set; }
    
    public string Status { get; set; } = "Active"; // Active, Inactive, Suspended
}

public class SellerPayout : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    public string PayoutNumber { get; set; } = string.Empty;
    
    public int? BankAccountId { get; set; }
    public SellerBankAccount? BankAccount { get; set; }
    
    // Payout Details
    public decimal TotalEarnings { get; set; }
    public decimal PlatformCommission { get; set; }
    public decimal PlatformFees { get; set; }
    public decimal RefundAmount { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal NetPayoutAmount { get; set; }
    
    // Period
    public DateTime PeriodStartDate { get; set; }
    public DateTime PeriodEndDate { get; set; }
    
    // Status
    public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Failed, Cancelled
    public string? FailureReason { get; set; }
    
    // Payment Details
    public string? PaymentMethod { get; set; }
    public string? TransactionId { get; set; }
    public string? PaymentReference { get; set; }
    
    // CreatedAt inherited from BaseEntity
    public DateTime? ProcessedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ProcessedBy { get; set; }
    
    public string? Notes { get; set; }
}

public class SellerPerformanceMetric : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    // Time Period
    public DateTime MetricDate { get; set; }
    public string Period { get; set; } = "Daily"; // Daily, Weekly, Monthly
    
    // Sales Metrics
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public int CancelledOrders { get; set; }
    public int ReturnedOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal NetRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    
    // Product Metrics
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int OutOfStockProducts { get; set; }
    public int TotalViews { get; set; }
    public int TotalClicks { get; set; }
    public decimal ConversionRate { get; set; }
    
    // Customer Metrics
    public int UniqueCustomers { get; set; }
    public int RepeatCustomers { get; set; }
    public int NewCustomers { get; set; }
    
    // Rating & Reviews
    public decimal AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int PositiveReviews { get; set; }
    public int NegativeReviews { get; set; }
    
    // Service Metrics
    public decimal AverageShippingTime { get; set; } // in hours
    public decimal OnTimeDeliveryRate { get; set; } // percentage
    public decimal ResponseTime { get; set; } // in hours
    public decimal ResponseRate { get; set; } // percentage
    
    // Return & Refund Metrics
    public int TotalReturns { get; set; }
    public decimal ReturnRate { get; set; }
    public int TotalRefunds { get; set; }
    public decimal RefundAmount { get; set; }
    
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}

public class SellerSubscription : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    public string PlanName { get; set; } = string.Empty; // Basic, Pro, Premium, Enterprise
    public string PlanType { get; set; } = "Monthly"; // Monthly, Quarterly, Yearly
    
    // Pricing
    public decimal PlanPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    
    // Plan Features
    public int MaxProducts { get; set; }
    public decimal CommissionRate { get; set; } // Platform commission percentage
    public bool PrioritySupport { get; set; }
    public bool AdvancedAnalytics { get; set; }
    public bool CustomStorefront { get; set; }
    public bool FeaturedPlacement { get; set; }
    
    // Subscription Period
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AutoRenew { get; set; } = true;
    
    // Payment
    public string PaymentStatus { get; set; } = "Paid"; // Paid, Pending, Failed, Cancelled
    public string? PaymentTransactionId { get; set; }
    public DateTime? PaidAt { get; set; }
    
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
}

public class SellerFollower : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public DateTime FollowedAt { get; set; } = DateTime.UtcNow;
    
    // Notification Preferences
    public bool NotifyNewProducts { get; set; } = true;
    public bool NotifyPromotions { get; set; } = true;
    public bool NotifyRestocks { get; set; } = true;
}

public class SellerAnalyticsSummary : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    // Lifetime Stats
    public int LifetimeTotalOrders { get; set; }
    public decimal LifetimeTotalRevenue { get; set; }
    public int LifetimeTotalCustomers { get; set; }
    public int LifetimeTotalProducts { get; set; }
    
    // Current Month
    public int MonthOrders { get; set; }
    public decimal MonthRevenue { get; set; }
    public int MonthNewCustomers { get; set; }
    
    // Current Week
    public int WeekOrders { get; set; }
    public decimal WeekRevenue { get; set; }
    
    // Today
    public int TodayOrders { get; set; }
    public decimal TodayRevenue { get; set; }
    public int TodayVisitors { get; set; }
    
    // Rankings
    public int? SalesRanking { get; set; }
    public int? RatingRanking { get; set; }
    public int? PopularityRanking { get; set; }
    
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;
}
