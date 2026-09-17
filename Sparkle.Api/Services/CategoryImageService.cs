using Sparkle.Domain.Catalog;

namespace Sparkle.Api.Services;

/// <summary>
/// Service to manage category images and default icons
/// Provides fallback icons when no custom image is uploaded
/// </summary>
public interface ICategoryImageService
{
    /// <summary>
    /// Gets the image URL for a category (custom or default)
    /// </summary>
    string GetCategoryImageUrl(Category category);

    /// <summary>
    /// Gets the default icon Bootstrap class for a category slug
    /// </summary>
    (string Icon, string ColorClass) GetDefaultIcon(string? slug);

    /// <summary>
    /// Gets localized category title
    /// </summary>
    (string En, string Bn) GetCategoryTitle(string? slug, string defaultName);
}

public class CategoryImageService : ICategoryImageService
{
    private readonly Dictionary<string, (string Icon, string ColorClass)> _categoryIconMap = 
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["electronics-gadgets"] = ("bi-laptop", "text-primary"),
        ["fashion-lifestyle"] = ("bi-bag-heart", "text-danger"),
        ["home-living"] = ("bi-house-heart", "text-success"),
        ["mobiles-tablets"] = ("bi-phone", "text-info"),
        ["groceries-essentials"] = ("bi-basket", "text-warning"),
        ["beauty-personal-care"] = ("bi-heart-pulse", "text-danger"),
        ["sports-outdoors"] = ("bi-bicycle", "text-success"),
        ["laptops-computers"] = ("bi-pc-display", "text-primary"),
        ["automotive-bike"] = ("bi-car-front", "text-dark"),
        ["pet-supplies"] = ("bi-emoji-smile", "text-warning"),
        ["books-stationery"] = ("bi-book-half", "text-primary"),
        ["baby-kids-mom"] = ("bi-balloon-heart", "text-danger"),
        ["home-appliances"] = ("bi-plug", "text-secondary"),
        ["jewelry-accessories"] = ("bi-gem", "text-warning"),
        ["kitchen-small-appliances"] = ("bi-cup-hot", "text-danger"),
        ["local-bd-brands"] = ("bi-geo-alt", "text-success"),
        ["medicine-wellness"] = ("bi-capsule", "text-success"),
        ["travel-luggage"] = ("bi-suitcase", "text-info"),
        ["toys-games"] = ("bi-controller", "text-warning"),
        ["photography-camera"] = ("bi-camera", "text-info"),
        ["art-crafts"] = ("bi-palette", "text-warning"),
        ["musical-instruments"] = ("bi-music-note-beamed", "text-primary"),
        ["garden-outdoor"] = ("bi-tree", "text-success"),
        ["fitness-gym"] = ("bi-heart-pulse", "text-danger")
    };

    private readonly Dictionary<string, (string En, string Bn)> _categoryTitleMap = 
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["electronics-gadgets"] = ("Electronics & Gadgets", "ইলেকট্রনিক্স ও গ্যাজেটস"),
        ["mobiles-tablets"] = ("Mobiles & Tablets", "মোবাইল ও ট্যাবলেট"),
        ["laptops-computers"] = ("Laptops & Computers", "ল্যাপটপ ও কম্পিউটার"),
        ["fashion-lifestyle"] = ("Fashion & Lifestyle", "ফ্যাশন ও লাইফস্টাইল"),
        ["beauty-personal-care"] = ("Beauty & Personal Care", "বিউটি ও পার্সোনাল কেয়ার"),
        ["home-living"] = ("Home & Living", "হোম ও লিভিং"),
        ["home-appliances"] = ("Home Appliances", "হোম অ্যাপ্লায়েন্স"),
        ["kitchen-small-appliances"] = ("Kitchen Appliances", "কিচেন অ্যাপ্লায়েন্স"),
        ["groceries-essentials"] = ("Groceries & Essentials", "গ্রোসারি ও নিত্যপ্রয়োজনীয়"),
        ["baby-kids-mom"] = ("Baby, Kids & Mom", "বেবি, কিডস ও মম"),
        ["toys-games"] = ("Toys & Games", "টয়স ও গেমস"),
        ["books-stationery"] = ("Books & Stationery", "বই ও স্টেশনারি"),
        ["sports-outdoors"] = ("Sports & Outdoors", "স্পোর্টস ও আউটডোর"),
        ["automotive-bike"] = ("Automotive & Bike", "অটোমোটিভ ও বাইক"),
        ["pet-supplies"] = ("Pet Supplies", "পেট সাপ্লাইস"),
        ["jewelry-accessories"] = ("Jewelry & Accessories", "জুয়েলারি ও এক্সেসরিজ"),
        ["local-bd-brands"] = ("Local BD Brands", "লোকাল বাংলাদেশি ব্র্যান্ড"),
        ["medicine-wellness"] = ("Medicine & Wellness", "মেডিসিন ও ওয়েলনেস"),
        ["travel-luggage"] = ("Travel & Luggage", "ট্রাভেল ও লাগেজ"),
        ["photography-camera"] = ("Photography & Camera", "ফটোগ্রাফি ও ক্যামেরা"),
        ["art-crafts"] = ("Art & Crafts", "আর্ট ও ক্রাফটস"),
        ["musical-instruments"] = ("Musical Instruments", "মিউজিক্যাল ইন্সট্রুমেন্টস"),
        ["garden-outdoor"] = ("Garden & Outdoor", "গার্ডেন ও আউটডোর"),
        ["fitness-gym"] = ("Fitness & Gym", "ফিটনেস ও জিম")
    };

    /// <summary>
    /// Gets the image URL for a category.
    /// If a custom image URL exists, returns it.
    /// Otherwise returns a default placeholder icon URL.
    /// </summary>
    public string GetCategoryImageUrl(Category category)
    {
        // If admin has uploaded a custom image, use it
        if (!string.IsNullOrEmpty(category.ImageUrl))
        {
            return category.ImageUrl;
        }

        // Otherwise, return a default icon-based placeholder
        // This could be enhanced to return actual icon SVG URLs instead
        var (icon, _) = GetDefaultIcon(category.Slug);
        return $"/images/default-category-icon.svg?icon={Uri.EscapeDataString(icon)}";
    }

    /// <summary>
    /// Gets the default Bootstrap icon and color class for a category
    /// </summary>
    public (string Icon, string ColorClass) GetDefaultIcon(string? slug)
    {
        if (string.IsNullOrEmpty(slug))
            return ("bi-grid-1x2", "text-muted");

        return _categoryIconMap.TryGetValue(slug, out var info)
            ? info
            : ("bi-grid-1x2", "text-muted");
    }

    /// <summary>
    /// Gets localized category title (English and Bengali)
    /// </summary>
    public (string En, string Bn) GetCategoryTitle(string? slug, string defaultName)
    {
        if (string.IsNullOrEmpty(slug))
            return (defaultName, defaultName);

        return _categoryTitleMap.TryGetValue(slug, out var titles)
            ? titles
            : (defaultName, defaultName);
    }
}
