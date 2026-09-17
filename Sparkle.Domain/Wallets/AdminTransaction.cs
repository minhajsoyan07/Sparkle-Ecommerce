using Sparkle.Domain.Common;

namespace Sparkle.Domain.Wallets;

public class AdminTransaction : BaseEntity
{
    public decimal Amount { get; set; }
    public decimal BalanceBefore { get; set; }
    public decimal BalanceAfter { get; set; }
    
    public string TransactionType { get; set; } = "Commission"; // Commission, Payout, Refund
    public string Description { get; set; } = string.Empty;
    
    public string? ReferenceType { get; set; } // Order, Withdrawal
    public string? ReferenceId { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}
