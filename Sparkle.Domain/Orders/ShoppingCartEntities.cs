using Sparkle.Domain.Common;
using Sparkle.Domain.Catalog;

namespace Sparkle.Domain.Orders;

public class ShoppingCartItem : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = null!;
    
    public int Quantity { get; set; } = 1;
    
    public string? CouponCode { get; set; }
    
    public DateTime ReservedUntil { get; set; } = DateTime.UtcNow.AddMinutes(30);
}