using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Sellers;
using Sparkle.Domain.Catalog;

namespace Sparkle.Domain.System;

/// <summary>
/// Platform-wide announcements from Admin
/// </summary>
public class PlatformAnnouncement : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    
    public string TargetAudience { get; set; } = "All"; // All, Users, Sellers
    public string Priority { get; set; } = "Normal"; // Low, Normal, High, Urgent
    public string Type { get; set; } = "Info"; // Info, Warning, Promotion, Maintenance
    
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    
    public bool IsActive { get; set; } = true;
    public bool IsDismissible { get; set; } = true;
    public bool ShowOnDashboard { get; set; } = true;
    public bool ShowAsPopup { get; set; } = false;
    
    public string? ActionUrl { get; set; }
    public string? ActionText { get; set; }
    
    public int ViewCount { get; set; }
    public int DismissCount { get; set; }
    
    // CreatedBy and CreatedAt inherited from BaseEntity
}

/// <summary>
/// Track which users have dismissed announcements
/// </summary>
public class AnnouncementDismissal : BaseEntity
{
    public int AnnouncementId { get; set; }
    public virtual PlatformAnnouncement Announcement { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;
    
    public DateTime DismissedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Store/Shop visitor tracking for sellers to see who viewed their store
/// </summary>
public class StoreVisit : BaseEntity
{
    public int SellerId { get; set; }
    public virtual Seller Seller { get; set; } = null!;
    
    public string? UserId { get; set; } // Null for guests
    public virtual ApplicationUser? User { get; set; }
    
    public string? SessionId { get; set; } // For guest tracking
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? Referrer { get; set; }
    
    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;
    public int PageViews { get; set; } = 1;
    public int TimeSpentSeconds { get; set; }
    
    // What they looked at
    public int ProductsViewed { get; set; }
    public bool AddedToCart { get; set; }
    public bool MadePurchase { get; set; }
}

/// <summary>
/// Seller to Seller messaging for collaboration
/// </summary>
public class SellerMessage : BaseEntity
{
    public int SenderSellerId { get; set; }
    public virtual Seller SenderSeller { get; set; } = null!;
    
    public int ReceiverSellerId { get; set; }
    public virtual Seller ReceiverSeller { get; set; } = null!;
    
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
    
    public int? ParentMessageId { get; set; } // For threading
    public virtual SellerMessage? ParentMessage { get; set; }
    
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Admin direct message to user or seller
/// </summary>
public class AdminMessage : BaseEntity
{
    public string AdminUserId { get; set; } = string.Empty;
    public virtual ApplicationUser AdminUser { get; set; } = null!;
    
    public string? RecipientUserId { get; set; }
    public virtual ApplicationUser? RecipientUser { get; set; }
    
    public int? RecipientSellerId { get; set; }
    public virtual Seller? RecipientSeller { get; set; }
    
    public string Subject { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "General"; // General, Warning, AccountIssue, Promotion
    
    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }
    
    public bool RequiresResponse { get; set; } = false;
    public string? ResponseContent { get; set; }
    public DateTime? RespondedAt { get; set; }
    
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User's saved/favorite sellers
/// </summary>
public class FavoriteSeller : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public virtual ApplicationUser User { get; set; } = null!;
    
    public int SellerId { get; set; }
    public virtual Seller Seller { get; set; } = null!;
    
    public string? Note { get; set; } // Personal note about why they saved this seller
    public DateTime SavedAt { get; set; } = DateTime.UtcNow;
}
