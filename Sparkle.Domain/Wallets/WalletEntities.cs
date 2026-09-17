using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;

namespace Sparkle.Domain.Wallets;

public class UserWallet : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;
    
    public decimal Balance { get; set; } = 0;
    public string Currency { get; set; } = "BDT";
    
    public bool IsActive { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    
    public virtual ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
}

public class SellerWallet : BaseEntity
{
    public int SellerId { get; set; }
    public virtual Sparkle.Domain.Sellers.Seller Seller { get; set; } = null!;
    
    public decimal AvailableBalance { get; set; } = 0;
    public decimal PendingBalance { get; set; } = 0; // Orders not yet delivered
    public decimal TotalEarnings { get; set; } = 0;
    public decimal TotalWithdrawn { get; set; } = 0;
    
    public string Currency { get; set; } = "BDT";
    public bool IsActive { get; set; } = true;
    
    public virtual ICollection<WalletTransaction> Transactions { get; set; } = new List<WalletTransaction>();
    public virtual ICollection<WithdrawalRequest> WithdrawalRequests { get; set; } = new List<WithdrawalRequest>();
}

public class WalletTransaction : BaseEntity
{
    public string? UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }
    
    public int? SellerId { get; set; }
    public virtual Sparkle.Domain.Sellers.Seller? Seller { get; set; }
    
    public string TransactionType { get; set; } = string.Empty; // Credit, Debit
    public string Source { get; set; } = string.Empty; // Refund, OrderEarning, Withdrawal, TopUp, Cashback
    
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    
    public string? ReferenceType { get; set; } // Order, Refund, etc.
    public string? ReferenceId { get; set; }
    
    public string Status { get; set; } = "Completed"; // Pending, Completed, Failed
    public string? Description { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}

public class WithdrawalRequest : BaseEntity
{
    public int SellerId { get; set; }
    public virtual Sparkle.Domain.Sellers.Seller Seller { get; set; } = null!;
    
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected, Processed
    
    // Bank Details
    public string BankName { get; set; } = string.Empty;
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountHolderName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string? RoutingNumber { get; set; }
    
    // Processing
    public string? ProcessedBy { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public string? RejectionReason { get; set; }
    public string? TransactionReference { get; set; }
    
    public DateTime RequestDate { get; set; } = DateTime.UtcNow;
}

// ==================== LEDGER SYSTEM ====================

public enum LedgerEntryType
{
    OrderPayment = 0,
    Refund = 1,
    SellerPayout = 2,
    PlatformCommission = 3,
    DeliveryCharge = 4,
    Penalty = 5,
    Adjustment = 6,
    Cashback = 7,
    TopUp = 8
}

public class LedgerEntry : BaseEntity
{
    public string EntryNumber { get; set; } = string.Empty;
    
    public int? OrderId { get; set; }
    public int? SellerId { get; set; }
    public string? UserId { get; set; }
    
    public LedgerEntryType EntryType { get; set; }
    public string TransactionType { get; set; } = "Credit"; // Credit, Debit
    
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    
    public string? Reference { get; set; }
    public string? Description { get; set; }
    
    // Escrow tracking
    public bool IsEscrowHeld { get; set; }
    public DateTime? EscrowReleasedAt { get; set; }
    
    public DateTime EntryDate { get; set; } = DateTime.UtcNow;
}

