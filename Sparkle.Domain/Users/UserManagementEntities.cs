using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Catalog;

namespace Sparkle.Domain.Users;

public class UserProfile : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string? Avatar { get; set; }
    public string? Bio { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Gender { get; set; }
    public string? Occupation { get; set; }
    
    // Contact
    public string? PrimaryPhone { get; set; }
    public string? SecondaryPhone { get; set; }
    public bool IsPhoneVerified { get; set; }
    public bool IsEmailVerified { get; set; }
    
    // Preferences
    public string? PreferredLanguage { get; set; } = "bn";
    public string? PreferredCurrency { get; set; } = "BDT";
    public string? TimeZone { get; set; } = "Asia/Dhaka";
    
    // Stats
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
    public int TotalReviews { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastOrderAt { get; set; }
    
    // Loyalty
    public int LoyaltyPoints { get; set; }
    public string MembershipTier { get; set; } = "Bronze"; // Bronze, Silver, Gold, Platinum
    
    public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();
    public ICollection<UserWishlistItem> WishlistItems { get; set; } = new List<UserWishlistItem>();
    public ICollection<UserSearchHistory> SearchHistory { get; set; } = new List<UserSearchHistory>();
}

public class UserAddress : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string AddressType { get; set; } = "Home"; // Home, Office, Other
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string AlternatePhone { get; set; } = string.Empty;
    
    // Address Details
    public string AddressLine1 { get; set; } = string.Empty;
    public string AddressLine2 { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty; // Dhaka, Chittagong, etc.
    public string PostalCode { get; set; } = string.Empty;
    public string Country { get; set; } = "Bangladesh";
    
    // Location
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Landmark { get; set; }
    
    public bool IsDefault { get; set; }
    public bool IsDefaultBilling { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UserWishlistItem : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public decimal PriceWhenAdded { get; set; }
    public string? Notes { get; set; }
    
    // Notifications
    public bool NotifyOnPriceDrop { get; set; } = true;
    public bool NotifyOnBackInStock { get; set; } = true;
}

public class UserSearchHistory : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string SearchQuery { get; set; } = string.Empty;
    public string? SearchFilters { get; set; } // JSON
    public int ResultCount { get; set; }
    public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    
    public int? ClickedProductId { get; set; }
    public bool ConvertedToPurchase { get; set; }
}

public class UserNotificationSettings : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    // Email Notifications
    public bool EmailOrderUpdates { get; set; } = true;
    public bool EmailPromotions { get; set; } = true;
    public bool EmailNewsletter { get; set; } = true;
    public bool EmailPriceDrops { get; set; } = true;
    public bool EmailReviewReminders { get; set; } = true;
    
    // SMS Notifications
    public bool SmsOrderUpdates { get; set; } = true;
    public bool SmsDeliveryUpdates { get; set; } = true;
    public bool SmsPromotions { get; set; } = false;
    
    // Push Notifications
    public bool PushOrderUpdates { get; set; } = true;
    public bool PushPromotions { get; set; } = true;
    public bool PushFlashDeals { get; set; } = true;
    
    // Communication Preferences
    public string PreferredContactMethod { get; set; } = "Email"; // Email, SMS, Push
    public bool DoNotDisturb { get; set; }
    public TimeSpan? DoNotDisturbStart { get; set; }
    public TimeSpan? DoNotDisturbEnd { get; set; }
}

public class UserDevice : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public string DeviceType { get; set; } = string.Empty; // Web, iOS, Android
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public string? DeviceModel { get; set; }
    public string? OperatingSystem { get; set; }
    public string? BrowserName { get; set; }
    public string? BrowserVersion { get; set; }
    
    public string? PushToken { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
}
