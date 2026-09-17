using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Catalog;

namespace Sparkle.Domain.System;

public class UserActivityLog : BaseEntity
{
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    public string ActivityType { get; set; } = string.Empty;
    
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? SessionId { get; set; }
    public string? RefererUrl { get; set; }
    public string? Metadata { get; set; }
}

public class SearchLog : BaseEntity
{
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    public string SearchQuery { get; set; } = string.Empty;
    public int? CategoryId { get; set; }
    public Category? Category { get; set; }
    
    public string? Filters { get; set; }
    public int ResultCount { get; set; }
    
    public int? ClickedProductId { get; set; }
    public Product? ClickedProduct { get; set; }
    
    public string? SessionId { get; set; }
    public string? IpAddress { get; set; }
    
    public string SearchType { get; set; } = "General";
}

public class ProductViewLog : BaseEntity
{
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    public string? UserId { get; set; }
    public ApplicationUser? User { get; set; }
    
    public string? ViewSource { get; set; }
    public string? IpAddress { get; set; }
    public string? DeviceType { get; set; }
    public string? RefererUrl { get; set; }
    public string? SessionId { get; set; }
    public int? DurationSeconds { get; set; }
}