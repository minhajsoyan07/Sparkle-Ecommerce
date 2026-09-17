namespace Sparkle.Domain.Orders;

public class ShippingAddress
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public required string FullName { get; set; }
    public required string PhoneNumber { get; set; }
    public string? AlternatePhone { get; set; }
    
    // Bangladesh Address System
    public int DivisionId { get; set; }
    public int DistrictId { get; set; }
    public int UpazilaId { get; set; }
    public int? UnionId { get; set; }
    
    public required string AddressLine { get; set; }
    public string? Landmark { get; set; }
    
    // Google Map Integration
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
