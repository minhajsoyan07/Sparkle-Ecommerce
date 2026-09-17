using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Sellers;

namespace Sparkle.Domain.Support;

// ==================== DISPUTE SYSTEM ====================

public enum DisputeType
{
    DeliveryIssue = 0,
    ProductQuality = 1,
    RefundDelay = 2,
    SellerMisconduct = 3,
    MissingItem = 4,
    WrongProduct = 5,
    DamagedProduct = 6,
    FakeProduct = 7,
    PricingIssue = 8,
    Other = 99
}

public enum DisputeStatus
{
    Opened = 0,
    UnderInvestigation = 1,
    EvidenceReview = 2,
    WaitingForSeller = 3,
    WaitingForCustomer = 4,
    Resolved = 5,
    Rejected = 6,
    Escalated = 7,
    Closed = 8
}

public enum DisputeResolution
{
    None = 0,
    FullRefund = 1,
    PartialRefund = 2,
    Replacement = 3,
    StoreCredit = 4,
    NoAction = 5,
    SellerPenalty = 6
}

public class Dispute : BaseEntity
{
    public string DisputeNumber { get; set; } = string.Empty;
    
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public int? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    
    // Dispute Details
    public DisputeType Type { get; set; }
    public DisputeStatus Status { get; set; } = DisputeStatus.Opened;
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Evidence
    public string? Evidence { get; set; } // JSON: [{type: "image", url: "..."}, ...]
    public string? SellerResponse { get; set; }
    public string? SellerEvidence { get; set; }
    
    // Assignment
    public string? AssignedTo { get; set; } // Admin user ID
    public DateTime? AssignedAt { get; set; }
    
    // Resolution
    public DisputeResolution ResolutionType { get; set; } = DisputeResolution.None;
    public string? ResolutionDetails { get; set; }
    public decimal? RefundAmount { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolvedBy { get; set; }
    
    // Escalation
    public bool IsEscalated { get; set; }
    public string? EscalationReason { get; set; }
    public DateTime? EscalatedAt { get; set; }
    
    // Priority
    public string Priority { get; set; } = "Medium"; // Low, Medium, High, Urgent
    
    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }
    
    public ICollection<DisputeNote> Notes { get; set; } = new List<DisputeNote>();
}

public class DisputeNote : BaseEntity
{
    public int DisputeId { get; set; }
    public Dispute Dispute { get; set; } = null!;
    
    public string AuthorId { get; set; } = string.Empty;
    public ApplicationUser? Author { get; set; }
    
    public string Content { get; set; } = string.Empty;
    public bool IsInternal { get; set; } // Admin-only notes
    public bool IsSystemGenerated { get; set; }
    
    public string? Attachments { get; set; } // JSON array
    
    public DateTime PostedAt { get; set; } = DateTime.UtcNow;
}

// ==================== SELLER PENALTY SYSTEM ====================

public enum PenaltyReason
{
    LateDispatch = 0,
    FakeProduct = 1,
    HighReturnRate = 2,
    CustomerComplaints = 3,
    PolicyViolation = 4,
    QCFailure = 5,
    MissingItems = 6,
    WrongProduct = 7,
    Other = 99
}

public enum PenaltyStatus
{
    Applied = 0,
    Appealed = 1,
    Waived = 2,
    Deducted = 3
}

public class SellerPenalty : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    
    public int? DisputeId { get; set; }
    public Dispute? Dispute { get; set; }
    
    public PenaltyReason Reason { get; set; }
    public PenaltyStatus Status { get; set; } = PenaltyStatus.Applied;
    
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    
    // Appeal
    public string? AppealReason { get; set; }
    public DateTime? AppealedAt { get; set; }
    public string? AppealDecision { get; set; }
    public DateTime? AppealDecidedAt { get; set; }
    
    // Deduction
    public bool IsDeducted { get; set; }
    public DateTime? DeductedAt { get; set; }
    public int? DeductedFromPayoutId { get; set; }
    
    public string? AppliedBy { get; set; }
}
