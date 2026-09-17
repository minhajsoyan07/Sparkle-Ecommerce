namespace Sparkle.Domain.Marketing;

public class Discount
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DiscountPercentage { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
    public int? VendorId { get; set; } // Null for admin-wide discounts
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
