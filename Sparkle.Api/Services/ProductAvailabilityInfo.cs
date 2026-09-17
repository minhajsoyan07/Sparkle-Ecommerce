namespace Sparkle.Api.Services;

/// <summary>
/// DTO for product availability information across all variants
/// Ensures consistent stock information throughout the application
/// </summary>
public class ProductAvailabilityInfo
{
 /// <summary>
    /// Total stock across all variants
    /// </summary>
    public int TotalStock { get; set; }

    /// <summary>
    /// Number of variants with available stock
    /// </summary>
    public int AvailableVariants { get; set; }

    /// <summary>
    /// Total number of variants
    /// </summary>
  public int TotalVariants { get; set; }

/// <summary>
    /// Is product completely out of stock
    /// </summary>
    public bool IsOutOfStock { get; set; }

    /// <summary>
    /// Is product running low on stock (<=10 items)
    /// </summary>
    public bool IsLowStock { get; set; }

    /// <summary>
    /// Human-readable status
    /// </summary>
    public string Status { get; set; } = string.Empty;
}
