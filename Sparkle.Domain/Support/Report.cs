using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Sellers;

namespace Sparkle.Domain.Support;

/// <summary>
/// Represents a report submitting by a user against a product or seller.
/// </summary>
public class Report : BaseEntity
{
    public string ReporterId { get; set; } = string.Empty;
    public ApplicationUser Reporter { get; set; } = null!;

    // Target Type: "Product" or "Seller"
    public string TargetType { get; set; } = "Product";
    
    // If reporting a product
    public int? ProductId { get; set; }
    public Product? Product { get; set; }

    // If reporting a seller
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }

    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // Status: Pending, Reviewed, Resolved, Dismissed
    public string Status { get; set; } = "Pending";
    

    public DateTime? ResolvedAt { get; set; }
    
    public string? ResolutionNotes { get; set; }
}
