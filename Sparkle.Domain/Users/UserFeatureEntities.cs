using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Catalog;

namespace Sparkle.Domain.Users;

/// <summary>
/// Tracks products that users have recently viewed
/// </summary>
public class RecentlyViewedItem : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;
    
    public int ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;
    
    public DateTime ViewedAt { get; set; } = DateTime.UtcNow;
    public int ViewCount { get; set; } = 1;
}

/// <summary>
/// Products added to comparison list
/// </summary>
public class CompareListItem : BaseEntity
{
    public string? UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }
    
    public string? SessionId { get; set; } // For guest users
    
    public int ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;
    
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Stock notification request - notify when product is back in stock
/// </summary>
public class StockNotification : BaseEntity
{
    public string? UserId { get; set; }
    public virtual ApplicationUser? User { get; set; }
    
    public string Email { get; set; } = string.Empty;
    
    public int ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;
    
    public int? VariantId { get; set; }
    public virtual ProductVariant? Variant { get; set; }
    
    public bool IsNotified { get; set; } = false;
    public DateTime? NotifiedAt { get; set; }
    
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Price drop alert - notify when product price drops below target
/// </summary>
public class PriceAlert : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;
    
    public int ProductId { get; set; }
    public virtual Product Product { get; set; } = null!;
    
    public decimal TargetPrice { get; set; }
    public decimal OriginalPrice { get; set; }
    
    public bool IsActive { get; set; } = true;
    public bool IsNotified { get; set; } = false;
    public DateTime? NotifiedAt { get; set; }
    
    // CreatedAt inherited from BaseEntity
}
