using Sparkle.Domain.Common;

namespace Sparkle.Domain.Configuration;

public class CommissionConfig : BaseEntity
{
    // Global default commission rate
    public decimal GlobalRate { get; set; } = 15.0m; // Default 15%
    
    // Category-specific rates
    public string CategoryRates { get; set; } = "{}"; // JSON: {"categoryId": rate}
    
    // Seller-specific overrides
    public string SellerRates { get; set; } = "{}"; // JSON: {"sellerId": rate}
    
    // Effective date
    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    
    // Description/notes
    public string? Notes { get; set; }
    
    // Active status
    public bool IsActive { get; set; } = true;
}
