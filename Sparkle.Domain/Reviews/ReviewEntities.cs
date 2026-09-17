using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Sellers;

namespace Sparkle.Domain.Reviews;

public class ProductReview : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }
    
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    
    // Review Content
    public int Rating { get; set; } // 1-5 stars
    public string Title { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    
    // Product Quality Ratings
    public int? QualityRating { get; set; }
    public int? ValueForMoneyRating { get; set; }
    public int? AccuracyRating { get; set; } // Matches description?
    
    // Verification
    public bool IsVerifiedPurchase { get; set; }
    public DateTime? PurchaseDate { get; set; }
    
    // Status
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string? RejectionReason { get; set; }
    public DateTime? ApprovedAt { get; set; }
    
    // Interactions
    public int HelpfulCount { get; set; }
    public int NotHelpfulCount { get; set; }
    public int ReportCount { get; set; }
    
    // Seller Response
    public string? SellerResponse { get; set; }
    public DateTime? SellerResponseDate { get; set; }
    
    // Admin Moderation
    public string? AdminNote { get; set; }
    public bool IsAdminNoteVisible { get; set; } = true; // Visible to seller/customer
    public bool IsLocked { get; set; } = false; // Prevent further edits
    
    // Seller Features
    public bool IsPinned { get; set; } = false; // Seller can highlight important feedback
    public DateTime? LastEditedAt { get; set; }
    public int EditCount { get; set; } = 0;
    
    public DateTime ReviewDate { get; set; } = DateTime.UtcNow;
    
    public ICollection<ReviewImage> Images { get; set; } = new List<ReviewImage>();
    public ICollection<ReviewVote> Votes { get; set; } = new List<ReviewVote>();
    public ICollection<ReviewEditHistory> EditHistory { get; set; } = new List<ReviewEditHistory>();
}

public class ReviewImage : BaseEntity
{
    public int ProductReviewId { get; set; }
    public ProductReview ProductReview { get; set; } = null!;
    
    public string ImageUrl { get; set; } = string.Empty;
    public string? Caption { get; set; }
    public int DisplayOrder { get; set; }
}

public class ReviewVote : BaseEntity
{
    public int ProductReviewId { get; set; }
    public ProductReview ProductReview { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public bool IsHelpful { get; set; } // true = helpful, false = not helpful
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;
}

public class ProductQuestion : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string Question { get; set; } = string.Empty;
    public string? Context { get; set; }
    
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime AskedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    
    public int AnswerCount { get; set; }
    public int UpvoteCount { get; set; }
    
    public ICollection<QuestionAnswer> Answers { get; set; } = new List<QuestionAnswer>();
}

public class QuestionAnswer : BaseEntity
{
    public int ProductQuestionId { get; set; }
    public ProductQuestion ProductQuestion { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    
    public string Answer { get; set; } = string.Empty;
    
    public bool IsSellerAnswer { get; set; }
    public bool IsVerifiedPurchaser { get; set; }
    
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    
    public int UpvoteCount { get; set; }
    public int DownvoteCount { get; set; }
}

/// <summary>
/// Tracks changes to reviews for audit and history purposes.
/// </summary>
public class ReviewEditHistory : BaseEntity
{
    public int ProductReviewId { get; set; }
    public ProductReview ProductReview { get; set; } = null!;
    
    public string? PreviousComment { get; set; }
    public string? NewComment { get; set; }
    public int? PreviousRating { get; set; }
    public int? NewRating { get; set; }
    
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    public string EditedBy { get; set; } = string.Empty; // UserId
    public string EditType { get; set; } = "Update"; // Update, SellerResponse, AdminAction
}

/// <summary>
/// Tracks product quality issues detected through reviews and reports.
/// Auto-generated when products fall below quality thresholds (e.g., < 2.5★ rating).
/// </summary>
public class ProductQualityIssue : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    // Issue Classification
    public QualityIssueType IssueType { get; set; }
    public QualityIssueSeverity Severity { get; set; }
    
    // Current Metrics
    public decimal CurrentRating { get; set; }
    public int TotalReviews { get; set; }
    public int LowRatingCount { get; set; } // Reviews ≤ 2.5★
    public int ReportCount { get; set; }
    public int RejectedReviewCount { get; set; }
    
    // Status & Resolution
    public QualityIssueStatus Status { get; set; } = QualityIssueStatus.Open;
    public string? AdminNotes { get; set; }
    public string? Resolution { get; set; }
    public string? ActionTaken { get; set; } // e.g., "Product suspended", "Seller contacted"
    
    // Timestamps
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    
    // Admin Tracking
    public string? ReviewedBy { get; set; } // Admin UserId
    public string? ResolvedBy { get; set; } // Admin UserId
    
    // Auto-Actions
    public bool AutoSuspended { get; set; } = false;
    public bool SellerNotified { get; set; } = false;
    public DateTime? SellerNotifiedAt { get; set; }
}

public enum QualityIssueType
{
    LowAverageRating,      // Average rating < 2.5★
    CriticalRating,        // Average rating < 2.0★
    HighReportCount,       // ≥3 customer reports
    ConsistentLowRatings,  // Recent reviews trend downward
    MultipleRejections     // Many reviews rejected for policy violations
}

public enum QualityIssueSeverity
{
    Low,       // Minor concern, monitor only
    Medium,    // Needs attention
    High,      // Urgent, contact seller
    Critical   // Auto-suspend, immediate action
}

public enum QualityIssueStatus
{
    Open,           // Newly detected, needs review
    InReview,       // Admin investigating
    AwaitingSeller, // Waiting for seller response
    Resolved,       // Issue fixed/product improved
    Ignored,        // False positive/acceptable
    Escalated       // Elevated to higher support tier
}

