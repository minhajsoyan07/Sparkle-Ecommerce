using Sparkle.Domain.Common;

namespace Sparkle.Domain.Wallets;

/// <summary>
/// Admin/Platform wallet to track total commission earnings
/// </summary>
public class AdminWallet : BaseEntity
{
    public decimal TotalCommissionEarned { get; set; } = 0;
    public decimal TotalRefunded { get; set; } = 0;
    public decimal TotalPayoutsToSellers { get; set; } = 0;
    public decimal CurrentBalance { get; set; } = 0;
    
    public string Currency { get; set; } = "BDT";
    
    // Analytics fields
    public decimal ThisMonthCommission { get; set; } = 0;
    public decimal LastMonthCommission { get; set; } = 0;
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}
