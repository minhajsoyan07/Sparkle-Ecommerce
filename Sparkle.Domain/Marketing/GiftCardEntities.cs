using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;

namespace Sparkle.Domain.Marketing;

/// <summary>
/// Gift Card / Store Credit
/// </summary>
public class GiftCard : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string? PIN { get; set; } // Optional PIN for security
    
    public decimal OriginalAmount { get; set; }
    public decimal Balance { get; set; }
    
    public string Status { get; set; } = "Active"; // Active, Used, Expired, Cancelled
    
    // Purchase Info
    public string? PurchasedByUserId { get; set; }
    public virtual ApplicationUser? PurchasedByUser { get; set; }
    public DateTime? PurchasedAt { get; set; }
    
    // Redemption Info
    public string? RedeemedByUserId { get; set; }
    public virtual ApplicationUser? RedeemedByUser { get; set; }
    public DateTime? RedeemedAt { get; set; }
    
    // Gift Details
    public string? RecipientName { get; set; }
    public string? RecipientEmail { get; set; }
    public string? Message { get; set; }
    public DateTime? DeliverAt { get; set; } // Scheduled delivery
    public bool IsDelivered { get; set; } = false;
    
    // Configuration
    public bool IsTransferable { get; set; } = true;
    public bool IsReloadable { get; set; } = false;
    public DateTime? ExpiryDate { get; set; }
    
    // Tracking
    public virtual ICollection<GiftCardTransaction> Transactions { get; set; } = new List<GiftCardTransaction>();
}

/// <summary>
/// Gift Card Transaction History
/// </summary>
public class GiftCardTransaction : BaseEntity
{
    public int GiftCardId { get; set; }
    public virtual GiftCard GiftCard { get; set; } = null!;
    
    public string TransactionType { get; set; } = string.Empty; // Purchase, Redeem, Reload, Refund
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    
    public int? OrderId { get; set; }
    public string? Description { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Store Credit for customers
/// </summary>
public class StoreCredit : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;
    
    public decimal Balance { get; set; } = 0;
    public decimal TotalEarned { get; set; } = 0;
    public decimal TotalUsed { get; set; } = 0;
    
    public bool IsActive { get; set; } = true;
    
    public virtual ICollection<StoreCreditTransaction> Transactions { get; set; } = new List<StoreCreditTransaction>();
}

/// <summary>
/// Store Credit Transaction History
/// </summary>
public class StoreCreditTransaction : BaseEntity
{
    public int StoreCreditId { get; set; }
    public virtual StoreCredit StoreCredit { get; set; } = null!;
    
    public string TransactionType { get; set; } = string.Empty; // Earned, Used, Refund, Adjustment, Expired
    public string Source { get; set; } = string.Empty; // Refund, Promotion, Referral, Manual
    
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    
    public int? OrderId { get; set; }
    public string? ReferenceNumber { get; set; }
    public string? Description { get; set; }
    
    public DateTime? ExpiryDate { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}
