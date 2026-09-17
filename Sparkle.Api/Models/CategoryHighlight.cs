using System;
using System.Collections.Generic;
using System.Linq;

namespace Sparkle.Api.Models;

public class CategoryHighlight
{
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Icon { get; init; } = "bi-grid-1x2";
    public string AccentClass { get; init; } = "text-primary";
    public IReadOnlyList<string> BulletPoints { get; init; } = Array.Empty<string>();
}

public static class CategoryHighlightProvider
{
    public static readonly IReadOnlyList<CategoryHighlight> All = new List<CategoryHighlight>
    {
        new()
        {
            Slug = "electronics-gadgets",
            Title = "Electronics & Gadgets",
            Description = "Latest tech and electronic devices for modern living.",
            Icon = "bi-laptop",
            AccentClass = "text-primary",
            BulletPoints = new []
            {
                "Smartphones, tablets, and wearables",
                "Cameras, headphones, and audio",
                "Smart home devices and accessories"
            }
        },
        new()
        {
            Slug = "fashion-lifestyle",
            Title = "Fashion & Lifestyle",
            Description = "Trendy apparel and accessories for men, women, and kids.",
            Icon = "bi-bag-heart",
            AccentClass = "text-danger",
            BulletPoints = new []
            {
                "Clothing for all ages and occasions",
                "Shoes, bags, and fashion accessories",
                "Watches and lifestyle products"
            }
        },
        new()
        {
            Slug = "home-living",
            Title = "Home & Living",
            Description = "Transform your space with stylish home essentials.",
            Icon = "bi-house-door",
            AccentClass = "text-success",
            BulletPoints = new []
            {
                "Furniture and home decor",
                "Bedding, curtains, and textiles",
                "Storage and organization solutions"
            }
        },
        new()
        {
            Slug = "mobiles-tablets",
            Title = "Mobiles & Tablets",
            Description = "Latest smartphones and tablets from top brands.",
            Icon = "bi-phone",
            AccentClass = "text-info",
            BulletPoints = new []
            {
                "Android and iOS smartphones",
                "Tablets and e-readers",
                "Mobile accessories and cases"
            }
        },
        new()
        {
            Slug = "laptops-computers",
            Title = "Laptops & Computers",
            Description = "High-performance computing for work and gaming.",
            Icon = "bi-pc-display",
            AccentClass = "text-primary",
            BulletPoints = new []
            {
                "Laptops, desktops, and all-in-ones",
                "Gaming PCs and accessories",
                "Monitors, keyboards, and peripherals"
            }
        },
        new()
        {
            Slug = "home-appliances",
            Title = "Home Appliances",
            Description = "Essential appliances for modern homes.",
            Icon = "bi-house-gear",
            AccentClass = "text-warning",
            BulletPoints = new []
            {
                "Refrigerators and washing machines",
                "Air conditioners and fans",
                "Vacuum cleaners and irons"
            }
        },
        new()
        {
            Slug = "kitchen-small-appliances",
            Title = "Kitchen Appliances",
            Description = "Smart kitchen tools for effortless cooking.",
            Icon = "bi-cup-hot",
            AccentClass = "text-danger",
            BulletPoints = new []
            {
                "Blenders, mixers, and food processors",
                "Microwaves and rice cookers",
                "Coffee makers and toasters"
            }
        },
        new()
        {
            Slug = "beauty-personal-care",
            Title = "Beauty & Personal Care",
            Description = "Self-care essentials for wellness and beauty.",
            Icon = "bi-heart",
            AccentClass = "text-danger",
            BulletPoints = new []
            {
                "Skincare and cosmetics",
                "Hair care and styling products",
                "Personal care and grooming"
            }
        },
        new()
        {
            Slug = "jewelry-accessories",
            Title = "Jewelry & Accessories",
            Description = "Elegant jewelry and fashion accessories.",
            Icon = "bi-gem",
            AccentClass = "text-warning",
            BulletPoints = new []
            {
                "Gold, silver, and diamond jewelry",
                "Fashion and costume jewelry",
                "Watches and accessories"
            }
        },
        new()
        {
            Slug = "groceries-essentials",
            Title = "Groceries & Essentials",
            Description = "Daily necessities and grocery items.",
            Icon = "bi-basket",
            AccentClass = "text-success",
            BulletPoints = new []
            {
                "Fresh produce and dairy",
                "Packaged foods and snacks",
                "Household cleaning supplies"
            }
        },
        new()
        {
            Slug = "baby-kids-mom",
            Title = "Baby, Kids & Mom",
            Description = "Everything for baby care and maternal needs.",
            Icon = "bi-heart-fill",
            AccentClass = "text-info",
            BulletPoints = new []
            {
                "Baby clothing and accessories",
                "Diapers, feeding, and care products",
                "Maternity care products"
            }
        },
        new()
        {
            Slug = "toys-games",
            Title = "Toys & Games",
            Description = "Fun and educational toys for all ages.",
            Icon = "bi-puzzle",
            AccentClass = "text-primary",
            BulletPoints = new []
            {
                "Action figures and dolls",
                "Educational and STEM toys",
                "Board games and puzzles"
            }
        },
        new()
        {
            Slug = "books-stationery",
            Title = "Books & Stationery",
            Description = "Books, educational materials, and stationery.",
            Icon = "bi-book",
            AccentClass = "text-info",
            BulletPoints = new []
            {
                "Books across all genres",
                "Stationery and office supplies",
                "Educational materials and courses"
            }
        },
        new()
        {
            Slug = "sports-outdoors",
            Title = "Sports & Outdoors",
            Description = "Sports equipment and outdoor gear.",
            Icon = "bi-trophy",
            AccentClass = "text-success",
            BulletPoints = new []
            {
                "Sports equipment for all activities",
                "Camping and hiking gear",
                "Outdoor clothing and footwear"
            }
        },
        new()
        {
            Slug = "automotive-bike",
            Title = "Automotive & Bike",
            Description = "Vehicle parts and bike accessories.",
            Icon = "bi-car-front",
            AccentClass = "text-dark",
            BulletPoints = new []
            {
                "Car parts and accessories",
                "Bike parts and gear",
                "Car cleaning and maintenance"
            }
        },
        new()
        {
            Slug = "pet-supplies",
            Title = "Pet Supplies",
            Description = "Care products for your furry friends.",
            Icon = "bi-heart-pulse",
            AccentClass = "text-warning",
            BulletPoints = new []
            {
                "Pet food and treats",
                "Toys and accessories",
                "Health and grooming supplies"
            }
        },
        new()
        {
            Slug = "local-bd-brands",
            Title = "Local BD Brands",
            Description = "Authentic Bangladeshi products and brands.",
            Icon = "bi-flag",
            AccentClass = "text-success",
            BulletPoints = new []
            {
                "Handcrafted items and artisan goods",
                "Traditional Bangladeshi foods",
                "Bangladeshi clothing and textiles"
            }
        },
        new()
        {
            Slug = "medicine-wellness",
            Title = "Medicine & Wellness",
            Description = "Healthcare products and wellness essentials.",
            Icon = "bi-capsule",
            AccentClass = "text-danger",
            BulletPoints = new []
            {
                "Prescription and OTC medicines",
                "Vitamins and supplements",
                "Health monitoring equipment"
            }
        },
        new()
        {
            Slug = "photography-camera",
            Title = "Photography & Camera",
            Description = "Professional camera equipment and accessories.",
            Icon = "bi-camera2",
            AccentClass = "text-primary",
            BulletPoints = new []
            {
                "DSLR and mirrorless cameras",
                "Camera lenses and accessories",
                "Tripods, lighting, and storage"
            }
        },
        new()
        {
            Slug = "art-crafts",
            Title = "Art & Crafts",
            Description = "Creative supplies for artists and hobbyists.",
            Icon = "bi-brush",
            AccentClass = "text-info",
            BulletPoints = new []
            {
                "Painting and drawing supplies",
                "Crafting materials and tools",
                "DIY kits and projects"
            }
        },
        new()
        {
            Slug = "musical-instruments",
            Title = "Musical Instruments",
            Description = "Instruments and equipment for musicians.",
            Icon = "bi-music-note-beamed",
            AccentClass = "text-warning",
            BulletPoints = new []
            {
                "Guitars, keyboards, and drums",
                "Traditional instruments",
                "Audio equipment and accessories"
            }
        },
        new()
        {
            Slug = "garden-outdoor",
            Title = "Garden & Outdoor",
            Description = "Gardening tools and outdoor essentials.",
            Icon = "bi-flower3",
            AccentClass = "text-success",
            BulletPoints = new []
            {
                "Seeds, plants, and fertilizers",
                "Gardening tools and equipment",
                "Outdoor furniture and decor"
            }
        },
        new()
        {
            Slug = "travel-luggage",
            Title = "Travel & Luggage",
            Description = "Travel essentials for every journey.",
            Icon = "bi-suitcase-lg",
            AccentClass = "text-primary",
            BulletPoints = new []
            {
                "Suitcases and travel bags",
                "Backpacks and daypacks",
                "Travel accessories and organizers"
            }
        },
        new()
        {
            Slug = "fitness-gym",
            Title = "Fitness & Gym",
            Description = "Professional gym equipment and fitness gear.",
            Icon = "bi-heart-pulse",
            AccentClass = "text-danger",
            BulletPoints = new []
            {
                "Gym equipment and weights",
                "Fitness accessories and gear",
                "Supplements and nutrition"
            }
        }
    };

    public static CategoryHighlight? GetBySlug(string slug)
    {
        return All.FirstOrDefault(h => string.Equals(h.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }
}
