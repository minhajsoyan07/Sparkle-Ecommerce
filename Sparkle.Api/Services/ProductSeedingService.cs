using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Sellers;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Services;

public class ProductSeedingService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ProductSeedingService> _logger;

    public ProductSeedingService(ApplicationDbContext db, ILogger<ProductSeedingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task SeedProductsAsync()
    {
        // Performance indexes check independent of seeding logic
        await EnsurePerformanceIndexesAsync();

        // Ensure all sellers are approved/active so their products show up
        // REMOVED: Unconditional approval on startup breaks the 'Pending' workflow.
        // Existing 'Active' (1) sellers are automatically mapped to 'Approved' (1) by Enum integer value preservation.
        /*
        var inactiveSellers = await _db.Sellers.Where(s => s.Status != SellerStatus.Approved).ToListAsync();
        if (inactiveSellers.Any())
        {
            foreach (var s in inactiveSellers) s.Status = SellerStatus.Approved;
            await _db.SaveChangesAsync();
        }
        */

        // Check if products already exist
        // Check if products already exist (optimized to check limit only)
        var productsExist = await _db.Products.OrderBy(p => p.Id).Skip(20).AnyAsync();
        var dailyEssentialsExist = await _db.Products.AnyAsync(p => p.Seller != null && p.Seller.ShopName == "Daily Essentials BD");

        if (productsExist && dailyEssentialsExist)
        {
            _logger.LogInformation("Products already seeded. Checking Flash Sale items...");
            await EnsureFlashSaleProductsAsync();
            return;
        }

        _logger.LogInformation("Starting product seeding...");

        var categories = await _db.Categories.ToListAsync();
        var sellers = await _db.Sellers.ToListAsync();

        if (!categories.Any() || !sellers.Any())
        {
            _logger.LogWarning("No categories or sellers found. Cannot seed products.");
            return;
        }

        var products = new List<Product>();

        // Electronics & Gadgets Products
        var electronicsCategory = categories.FirstOrDefault(c => c.Slug == "electronics-gadgets");
        if (electronicsCategory != null && !await _db.Products.AnyAsync(p => p.CategoryId == electronicsCategory.Id))
        {
            products.AddRange(CreateElectronicsProducts(electronicsCategory, sellers));
        }

        // Mobile & Tablets Products
        var mobilesCategory = categories.FirstOrDefault(c => c.Slug == "mobiles-tablets");
        if (mobilesCategory != null && !await _db.Products.AnyAsync(p => p.CategoryId == mobilesCategory.Id))
        {
            products.AddRange(CreateMobileProducts(mobilesCategory, sellers));
        }

        // Fashion & Lifestyle Products
        var fashionCategory = categories.FirstOrDefault(c => c.Slug == "fashion-lifestyle");
        if (fashionCategory != null && !await _db.Products.AnyAsync(p => p.CategoryId == fashionCategory.Id))
        {
            products.AddRange(CreateFashionProducts(fashionCategory, sellers));
        }

        // Home & Living Products
        var homeCategory = categories.FirstOrDefault(c => c.Slug == "home-living");
        if (homeCategory != null && !await _db.Products.AnyAsync(p => p.CategoryId == homeCategory.Id))
        {
            products.AddRange(CreateHomeProducts(homeCategory, sellers));
        }

        // Daily Essentials BD Products
        // Daily Essentials BD Products
        // Find by shop name "Daily Essentials BD"
        var dailyEssentialsSeller = sellers.FirstOrDefault(s => s.ShopName == "Daily Essentials BD");
        
        // If not found, use the first seller (Seller 1) and rename/update it to match request
        if (dailyEssentialsSeller == null && sellers.Any())
        {
            dailyEssentialsSeller = sellers.First();
            dailyEssentialsSeller.ShopName = "Daily Essentials BD";
            dailyEssentialsSeller.ShopDescription = "Your one-stop shop for daily household needs, electronics, and kitchenware.";
            _logger.LogInformation($"Updated Seller 1 ({dailyEssentialsSeller.Email}) to 'Daily Essentials BD'");
        }

        // Check if the specific new products are already seeded (check one key product)
        bool dailyEssentialsProductsExist = await _db.Products.AnyAsync(p => p.Title == "RFL Water Jug Aqua Fresh 2.5L");

        if (dailyEssentialsSeller != null && !dailyEssentialsProductsExist)
        {
            products.AddRange(CreateDailyEssentialsProducts(categories, dailyEssentialsSeller));
            _logger.LogInformation("Added 30 products for Daily Essentials BD");
        }
        else if (dailyEssentialsProductsExist)
        {
             _logger.LogInformation("Daily Essentials BD products already seeded. Skipping.");
        }
        else
        {
             _logger.LogWarning("No sellers available to seed Daily Essentials products.");
        }

        // Daily Mart BD Products (Seller 2)
        // Find by shop name "DailyMart BD"
        var dailyMartSeller = sellers.FirstOrDefault(s => s.ShopName == "DailyMart BD");

        // If not found, check if mapped from Program.cs update or use second seller if available and not taken
        if (dailyMartSeller == null && sellers.Count > 1)
        {
            var potentialSeller = sellers.Skip(1).First();
            // Verify this isn't Daily Essentials (Seller 1)
            if (potentialSeller.ShopName != "Daily Essentials BD")
            {
                dailyMartSeller = potentialSeller;
                dailyMartSeller.ShopName = "DailyMart BD";
                dailyMartSeller.ShopDescription = "Everything you need for your daily life. Groceries, home appliances, and household essentials delivered to your door.";
                _logger.LogInformation($"Updated Seller 2 ({dailyMartSeller.Email}) to 'DailyMart BD'");
            }
        }

        bool dailyMartProductsExist = await _db.Products.AnyAsync(p => p.Title == "Walton Smart LED Bulb WBL-12W");

        if (dailyMartSeller != null && !dailyMartProductsExist)
        {
            products.AddRange(CreateDailyMartProducts(categories, dailyMartSeller));
            _logger.LogInformation("Added 30 products for DailyMart BD");
        }
        else if (dailyMartProductsExist)
        {
             _logger.LogInformation("DailyMart BD products already seeded. Skipping.");
        }
        else
        {
             _logger.LogWarning("No sellers available to seed DailyMart products.");
        }

        // HomeNeeds BD Products (Seller 3)
        // Find by shop name "HomeNeeds BD"
        var homeNeedsSeller = sellers.FirstOrDefault(s => s.ShopName == "HomeNeeds BD");

        // If not found, check if mapped from Program.cs update or use third seller if available and not taken
        if (homeNeedsSeller == null && sellers.Count > 2)
        {
            var potentialSeller = sellers.Skip(2).First();
            // Verify this isn't Seller 1 or 2
            if (potentialSeller.ShopName != "Daily Essentials BD" && potentialSeller.ShopName != "DailyMart BD")
            {
                homeNeedsSeller = potentialSeller;
                homeNeedsSeller.ShopName = "HomeNeeds BD";
                homeNeedsSeller.ShopDescription = "Quality home essentials, electronics, and personal care products. Committed to improving your daily living standards.";
                _logger.LogInformation($"Updated Seller 3 ({homeNeedsSeller.Email}) to 'HomeNeeds BD'");
            }
        }

        bool homeNeedsProductsExist = await _db.Products.AnyAsync(p => p.Title == "Walton Rechargeable Table Fan WRTF18");

        if (homeNeedsSeller != null && !homeNeedsProductsExist)
        {
            products.AddRange(CreateHomeNeedsProducts(categories, homeNeedsSeller));
            _logger.LogInformation("Added 30 products for HomeNeeds BD");
        }
        else if (homeNeedsProductsExist)
        {
             _logger.LogInformation("HomeNeeds BD products already seeded. Skipping.");
        }
        else
        {
             _logger.LogWarning("No sellers available to seed HomeNeeds products.");
        }

        // Add products to database (with their related images and variants)
        await _db.Products.AddRangeAsync(products);
        await _db.SaveChangesAsync();
        await EnsureFlashSaleProductsAsync();

        _logger.LogInformation($"Successfully seeded {products.Count} products with images and variants!");
    }

    private List<Product> CreateElectronicsProducts(Category category, List<Seller> sellers)
    {
        var products = new List<Product>();
        var random = new Random();
        int sortOrder = 0;

        // Product 1: Wireless Headphones
        var product1 = new Product
        {
            Title = "Sony WH-1000XM5 Wireless Noise Cancelling Headphones",
            Slug = "sony-wh-1000xm5-headphones",
            ShortDescription = "Industry-leading noise cancellation with premium sound quality",
            Description = "The Sony WH-1000XM5 headphones rewrite the rules for distraction-free listening. Two processors control 8 microphones for unprecedented noise cancellation. With up to 30 hours battery life, quick charging, LDAC codec support, and multipoint connection capability.",
            BasePrice = 32000,
            DiscountPercent = 15,
            CategoryId = category.Id,
            SellerId = sellers[random.Next(sellers.Count)].Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
        };

        product1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/1a1a1a/ffffff?text=Sony+WH-1000XM5+Black", SortOrder = sortOrder++ });
        product1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/f5f5f5/333333?text=Sony+Headphones+Side", SortOrder = sortOrder++ });
        
        product1.Variants.Add(new ProductVariant { Color = "Black", Price = 32000, Stock = 25, Sku = "SONY-WH-1000XM5-BLK" });
        product1.Variants.Add(new ProductVariant { Color = "Silver", Price = 32000, Stock = 15, Sku = "SONY-WH-1000XM5-SLV" });

        products.Add(product1);

        // Product 2: Smartwatch
        var product2 = new Product
        {
            Title = "Apple Watch Series 9 GPS 45mm",
            Slug = "apple-watch-series-9-gps-45mm",
            ShortDescription = "Advanced health tracking with bright always-on Retina display",
            Description = "Apple Watch Series 9 features the powerful S9 SiP. A magical new way to use your Apple Watch without touching the screen. A brighter display. And more health and safety features than ever.",
            BasePrice = 45000,
            DiscountPercent = 10,
            CategoryId = category.Id,
            SellerId = sellers[random.Next(sellers.Count)].Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
        };

        product2.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/000000/ffffff?text=Apple+Watch+Series+9", SortOrder = sortOrder++ });
        product2.Variants.Add(new ProductVariant { Color = "Midnight", Size = "45mm", Price = 45000, Stock = 30, Sku = "APPLE-WATCH-S9-45-MID" });

        products.Add(product2);

        return products;
    }

    private List<Product> CreateMobileProducts(Category category, List<Seller> sellers)
    {
        var products = new List<Product>();
        var random = new Random();
        int sortOrder = 0;

        // Product 1: Flagship Smartphone
        var product1 = new Product
        {
            Title = "Samsung Galaxy S24 Ultra 5G (12GB RAM, 256GB)",
            Slug = "samsung-galaxy-s24-ultra-12gb-256gb",
            ShortDescription = "Snapdragon 8 Gen 3, 200MP Camera, 5000mAh Battery",
            Description = "Meet the Galaxy S24 Ultra, the ultimate smartphone with a stunning 6.8-inch Dynamic AMOLED display, Snapdragon 8 Gen 3 processor, 200MP quad camera system, S Pen integration, and all-day 5000mAh battery with 45W super-fast charging.",
            BasePrice = 135000,
            DiscountPercent = 8,
            CategoryId = category.Id,
            SellerId = sellers[random.Next(sellers.Count)].Id,
            IsActive = true,
            AverageRating = 4.8m,
            TotalReviews = 245,
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
        };
        product1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/1a1a2e/ffffff?text=Galaxy+S24+Ultra", SortOrder = sortOrder++ });
        product1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/16213e/ffffff?text=S24+Camera", SortOrder = sortOrder++ });
        
        product1.Variants.Add(new ProductVariant { Color = "Titanium Black", Size = "256GB", Price = 135000, Stock = 40, Sku = "SAM-S24U-12-256-BLK" });
        product1.Variants.Add(new ProductVariant { Color = "Titanium Gray", Size = "256GB", Price = 135000, Stock = 35, Sku = "SAM-S24U-12-256-GRY" });

        products.Add(product1);

        // Product 2: Mid-range Smartphone
        var product2 = new Product
        {
            Title = "Xiaomi Redmi Note 13 Pro 5G (8GB RAM, 256GB)",
            Slug = "xiaomi-redmi-note-13-pro-5g",
            ShortDescription = "200MP Camera, 120Hz AMOLED Display, 67W Fast Charging",
            Description = "Experience flagship features at an affordable price. Redmi Note 13 Pro 5G features a stunning 200MP main camera, vibrant 120Hz AMOLED display, powerful MediaTek Dimensity 7200 Ultra processor, and lightning-fast 67W charging.",
            BasePrice = 32000,
            DiscountPercent = 12,
            CategoryId = category.Id,
            SellerId = sellers[random.Next(sellers.Count)].Id,
            IsActive = true,
            AverageRating = 4.5m,
            TotalReviews = 567,
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
        };
        product2.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/2d3561/ffffff?text=Redmi+Note+13+Pro", SortOrder = sortOrder++ });
        
        product2.Variants.Add(new ProductVariant { Color = "Midnight Black", Size = "256GB", Price = 32000, Stock = 50, Sku = "XIA-RN13P-8-256-BLK" });
        product2.Variants.Add(new ProductVariant { Color = "Ocean Blue", Size = "256GB", Price = 32000, Stock = 45, Sku = "XIA-RN13P-8-256-BLU" });

        products.Add(product2);

        return products;
    }

    private List<Product> CreateFashionProducts(Category category, List<Seller> sellers)
    {
        var products = new List<Product>();
        var random = new Random();
        int sortOrder = 0;

        // Product 1: Men's T-Shirt
        var product1 = new Product
        {
            Title = "Premium Cotton Round Neck T-Shirt for Men",
            Slug = "premium-cotton-round-neck-tshirt-men",
            ShortDescription = "100% Combed Cotton, Multiple Colors Available",
            Description = "Experience premium comfort with our 100% combed cotton t-shirt. Features breathable fabric, durable stitching, pre-shrunk material, and a perfect fit. Ideal for both casual and semi-formal occasions.",
            BasePrice = 599,
            DiscountPercent = 20,
            CategoryId = category.Id,
            SellerId = sellers[random.Next(sellers.Count)].Id,
            IsActive = true,
            AverageRating = 4.3m,
            TotalReviews = 1245,
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
        };
        product1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/1e3a8a/ffffff?text=Navy+T-Shirt", SortOrder = sortOrder++ });
        product1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/dc2626/ffffff?text=Red+T-Shirt", SortOrder = sortOrder++ });
        
        product1.Variants.Add(new ProductVariant { Color = "Navy Blue", Size = "M", Price = 599, Stock = 100, Sku = "TSHIRT-MEN-NAV-M" });
        product1.Variants.Add(new ProductVariant { Color = "Navy Blue", Size = "L", Price = 599, Stock = 85, Sku = "TSHIRT-MEN-NAV-L" });
        product1.Variants.Add(new ProductVariant { Color = "Black", Size = "M", Price = 599, Stock = 120, Sku = "TSHIRT-MEN-BLK-M" });
        product1.Variants.Add(new ProductVariant { Color = "White", Size = "L", Price = 599, Stock = 95, Sku= "TSHIRT-MEN-WHT-L" });

        products.Add(product1);

        return products;
    }

    private List<Product> CreateHomeProducts(Category category, List<Seller> sellers)
    {
        var products = new List<Product>();
        var random = new Random();
        int sortOrder = 0;

        // Product 1: Bedsheet Set
        var product1 = new Product
        {
            Title = "King Size Cotton Bedsheet Set with 2 Pillow Covers",
            Slug = "king-size-cotton-bedsheet-set",
            ShortDescription = "Premium quality double bedsheet, soft and comfortable",
            Description = "Transform your bedroom with this premium quality cotton bedsheet set. Includes 1 king-size bedsheet (90x100 inches) and 2 pillow covers (17x27 inches). Features vibrant colors, fade-resistant fabric, and easy maintenance.",
            BasePrice = 1899,
            DiscountPercent = 25,
            CategoryId = category.Id,
            SellerId = sellers[random.Next(sellers.Count)].Id,
            IsActive = true,
            AverageRating = 4.4m,
            TotalReviews = 892,
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
        };
        product1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/6366f1/ffffff?text=Blue+Bedsheet+Set", SortOrder = sortOrder++ });
        
        product1.Variants.Add(new ProductVariant { Color = "Blue Floral", Price = 1899, Stock = 60, Sku = "BEDSHEET-KING-BLUFL" });
        product1.Variants.Add(new ProductVariant { Color = "Pink Floral", Price = 1899, Stock = 55, Sku = "BEDSHEET-KING-PNKFL" });

        products.Add(product1);

        return products;
    }

    private List<Product> CreateDailyEssentialsProducts(List<Category> categories, Seller seller)
    {
        var products = new List<Product>();
        var random = new Random();


        // Helper to find category or default to first
        Category GetCategory(string slug) => categories.FirstOrDefault(c => c.Slug == slug) ?? categories.First();

        var household = GetCategory("home-living");
        var electronics = GetCategory("electronics-gadgets");
        var homeAppliance = GetCategory("home-appliances");
        var kitchen = GetCategory("kitchen-small-appliances");

        // 1. RFL Water Jug
        var p1 = new Product
        {
            Title = "RFL Water Jug Aqua Fresh 2.5L",
            Slug = "rfl-water-jug-aqua-fresh-2-5l",
            ShortDescription = "Durable 2.5L BPA-free plastic jug",
            Description = "Durable 2.5-liter water jug made from BPA-free plastic. Ideal for storing drinking water at home. Lightweight, easy to clean, and commonly used in kitchens and dining areas. Material: BPA-Free Plastic. Warranty: No Warranty.",
            BasePrice = 180,
            CategoryId = household.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.4 kg",
            Dimensions = "25 x 15 x 20 cm",
            Features = "BPA-free safe plastic|Lightweight and portable|Durable and long-lasting|Easy to clean and maintain",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/22d3ee/ffffff?text=RFL+Water+Jug", SortOrder = 0 });
        p1.Variants.Add(new ProductVariant { Stock = 200, Price = 180, Sku = "RFL-JUG-2.5L", Color = "Standard" });
        products.Add(p1);

        // 2. RFL Melody Stool
        var p2 = new Product
        {
            Title = "RFL Melody Plastic Stool (Medium)",
            Slug = "rfl-melody-plastic-stool-medium",
            ShortDescription = "Strong and stackable plastic stool",
            Description = "Lightweight, strong, and stackable plastic stool. Suitable for kitchens, washrooms, or casual seating at home. Resistant to moisture and daily wear. Material: Durable Plastic. Warranty: No Warranty.",
            BasePrice = 350,
            CategoryId = household.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "1.2 kg",
            Dimensions = "35 x 35 x 40 cm",
            Features = "Strong and sturdy construction|Moisture resistant|Stackable design|Lightweight and portable",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p2.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/f43f5e/ffffff?text=RFL+Stool", SortOrder = 0 });
        p2.Variants.Add(new ProductVariant { Stock = 150, Price = 350, Sku = "RFL-STOOL-MED", Color = "Red" });
        products.Add(p2);

        // 3. Walton Table Fan
        var p3 = new Product
        {
            Title = "Walton Rechargeable Table Fan WRTF24A",
            Slug = "walton-rechargeable-table-fan-wrtf24a",
            ShortDescription = "24-inch rechargeable fan with battery backup",
            Description = "High-airflow rechargeable table fan designed for emergency power situations. 24-inch blade, 3-speed control, and built-in battery backup. Perfect for bedrooms and offices during load shedding. Material: ABS Plastic + Metal. Warranty: 6 Months Warranty.",
            BasePrice = 3950,
            CategoryId = electronics.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "3.5 kg",
            Dimensions = "60 x 25 x 60 cm",
            Features = "Rechargeable battery backup|Three-speed airflow control|High airflow for cooling|Compact and portable design",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p3.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/3b82f6/ffffff?text=Walton+Fan", SortOrder = 0 });
        p3.Variants.Add(new ProductVariant { Stock = 80, Price = 3950, Sku = "WALT-FAN-24A", Color = "Blue" });
        products.Add(p3);

        // 4. Vision LED Bulb
        var p4 = new Product
        {
            Title = "Vision LED Bulb 12W Warm White",
            Slug = "vision-led-bulb-12w-warm-white",
            ShortDescription = "Energy-efficient 12W LED bulb",
            Description = "Energy-efficient 12W LED bulb with warm white illumination. Provides long-lasting light suitable for bedrooms, living rooms, or small office spaces. Material: Plastic + Electronic Components. Warranty: 1 Year Warranty.",
            BasePrice = 220,
            CategoryId = homeAppliance.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.15 kg",
            Dimensions = "10 x 5 x 5 cm",
            Features = "12W low-power consumption|Warm white light|Long-lasting lifespan|Easy to install",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p4.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/f59e0b/ffffff?text=Vision+LED", SortOrder = 0 });
        p4.Variants.Add(new ProductVariant { Stock = 250, Price = 220, Sku = "VIS-LED-12W-WW", Color = "Warm White" });
        products.Add(p4);

        // 5. Kiam Bowl Set
        var p5 = new Product
        {
            Title = "Kiam Stainless Steel Bowl Set (3pcs)",
            Slug = "kiam-stainless-steel-bowl-set-3pcs",
            ShortDescription = "Rust-resistant stainless steel bowls",
            Description = "Set of 3 stainless steel bowls, ideal for food preparation, mixing, or serving. Rust-resistant, durable, and suitable for daily kitchen use. Material: Stainless Steel. Warranty: No Warranty.",
            BasePrice = 480,
            CategoryId = kitchen.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "1.5 kg",
            Dimensions = "20 x 15 x 10 cm",
            Features = "Rust-resistant stainless steel|Durable and sturdy|Multi-purpose usage|Easy to clean",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p5.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/9ca3af/ffffff?text=Kiam+Bowls", SortOrder = 0 });
        p5.Variants.Add(new ProductVariant { Stock = 120, Price = 480, Sku = "KIAM-BOWL-3PCS", Color = "Silver" });
        products.Add(p5);

        // 6. Walton Room Heater
        var p6 = new Product
        {
            Title = "Walton Room Heater WRH-S20",
            Slug = "walton-room-heater-wrh-s20",
            ShortDescription = "Compact quartz room heater",
            Description = "Compact quartz room heater with dual heat settings, safe tip-over design, and energy-efficient operation. Perfect for winter use in bedrooms or small living areas. Material: Metal + Plastic. Warranty: 1 Year Warranty.",
            BasePrice = 2450,
            CategoryId = homeAppliance.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "2.2 kg",
            Dimensions = "35 x 20 x 15 cm",
            Features = "Dual heat settings|Energy-efficient heating|Tip-over safety design|Compact and portable",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p6.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/ef4444/ffffff?text=Walton+Heater", SortOrder = 0 });
        p6.Variants.Add(new ProductVariant { Stock = 75, Price = 2450, Sku = "WALT-HEAT-S20", Color = "Red" });
        products.Add(p6);

        // 7. RFL Dustbin
        var p7 = new Product
        {
            Title = "RFL Push-Lid Dustbin 10L",
            Slug = "rfl-push-lid-dustbin-10l",
            ShortDescription = "10L dustbin with push-lid system",
            Description = "10-liter durable dustbin with hygienic push-lid system. Suitable for home, kitchen, and office use. Easy to clean and move around. Material: Durable Plastic. Warranty: No Warranty.",
            BasePrice = 420,
            CategoryId = household.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.9 kg",
            Dimensions = "30 x 25 x 35 cm",
            Features = "Hygienic push-lid system|Durable plastic construction|Lightweight and portable|Easy to clean",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p7.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/10b981/ffffff?text=RFL+Dustbin", SortOrder = 0 });
        p7.Variants.Add(new ProductVariant { Stock = 180, Price = 420, Sku = "RFL-DUST-10L", Color = "Green" });
        products.Add(p7);

        // 8. Super Star Fan
        var p8 = new Product
        {
            Title = "Super Star Ceiling Fan SuperAir 56”",
            Slug = "super-star-ceiling-fan-superair-56",
            ShortDescription = "56-inch powerful ceiling fan",
            Description = "Affordable and powerful 56-inch ceiling fan. Ideal for daily household cooling. Energy-efficient with smooth operation and quiet motor. Material: Metal + Plastic. Warranty: 1 Year Warranty.",
            BasePrice = 2850,
            CategoryId = homeAppliance.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "6.0 kg",
            Dimensions = "56 x 56 x 20 cm",
            Features = "56-inch powerful fan|Energy-efficient motor|Quiet and smooth operation|Durable metal and plastic body",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p8.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/ffffff/000000?text=Super+Star+Fan", SortOrder = 0 });
        p8.Variants.Add(new ProductVariant { Stock = 60, Price = 2850, Sku = "SS-FAN-56", Color = "White" });
        products.Add(p8);

        // 9. RFL Bucket
        var p9 = new Product
        {
            Title = "RFL Water Bucket Premium 20L",
            Slug = "rfl-water-bucket-premium-20l",
            ShortDescription = "20L plastic bucket with handle",
            Description = "High-quality plastic bucket with sturdy handle and 20L water capacity. Lightweight, durable, and easy to carry. Perfect for daily household use. Material: Plastic. Warranty: No Warranty.",
            BasePrice = 190,
            CategoryId = household.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.8 kg",
            Dimensions = "35 x 35 x 30 cm",
            Features = "20L capacity|Durable and strong plastic|Easy-grip handle|Lightweight and portable",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p9.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/3b82f6/ffffff?text=RFL+Bucket", SortOrder = 0 });
        p9.Variants.Add(new ProductVariant { Stock = 220, Price = 190, Sku = "RFL-BUCKET-20L", Color = "Blue" });
        products.Add(p9);

        // 10. RFL Water Bottle
        var p10 = new Product
        {
            Title = "RFL Crystal Water Bottle 1L",
            Slug = "rfl-crystal-water-bottle-1l",
            ShortDescription = "1L BPA-free portable water bottle",
            Description = "1-liter BPA-free water bottle suitable for school, office, and daily use. Leak-proof and portable with durable plastic construction. Material: BPA-Free Plastic. Warranty: No Warranty.",
            BasePrice = 120,
            CategoryId = household.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.25 kg",
            Dimensions = "28 x 8 x 8 cm",
            Features = "BPA-free safe plastic|Leak-proof cap|Lightweight and portable|Durable and long-lasting",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 10))
        };
        p10.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/ec4899/ffffff?text=RFL+Bottle", SortOrder = 0 });
        p10.Variants.Add(new ProductVariant { Stock = 300, Price = 120, Sku = "RFL-BOTTLE-1L", Color = "Pink" });
        products.Add(p10);

        // --- Generated ~20 additional products for Daily Essentials ---
        
        string[] rflItems = { "RFL Wardrobe Double 5D", "RFL Dining Table 6 Seater", "RFL Easy Chair Premium", "RFL Chopping Board", "RFL Kitchen Rack 3-Tier", "RFL Spice Jar Set 6pcs", "RFL Bath Mug 1.5L", "RFL Hanger Set 12pcs", "RFL Cloth Clips 24pcs", "RFL Baby Potty Chair" };
        string[] elecItems = { "Walton 32\" LED TV", "Rice Cooker 2.8L", "Electric Kettle 1.5L", "Iron Philips Dry", "Blender 3-in-1 Vision", "Mosquito Bat Rechargeable", "Multi-plug 5 Sockets", "Extension Cord 5 Meters" };

        for (int i = 0; i < 20; i++)
        {
           string itemName;
           Category itemCat;
           decimal price;
           
           if (i < 10) {
              itemName = rflItems[i % rflItems.Length];
              itemCat = household;
              price = random.Next(150, 5000);
           } else {
              itemName = elecItems[i % elecItems.Length];
              itemCat = (itemName.Contains("Rice") || itemName.Contains("Blender") || itemName.Contains("Kettle")) ? kitchen : electronics;
              price = random.Next(300, 15000);
           }

           var genProduct = new Product
           {
               Title = itemName,
               Slug = itemName.ToLower().Replace(" ", "-").Replace("\"", "") + $"-{random.Next(1000,9999)}",
               ShortDescription = $"High quality {itemName} for daily use",
               Description = $"{itemName} is known for its durability and premium quality. Essential for every modern home. Comes with standard warranty where applicable. Material: Mixed Materials. Warranty: Service Warranty.",
               BasePrice = price,
               CategoryId = itemCat.Id,
               SellerId = seller.Id,
               IsActive = true,
               Weight = $"{random.Next(1, 5)} kg",
               Dimensions = "Standard",
               Features = "Premium Quality|Durable|Affordable|Authentic",
               CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 60))
           };
           
           genProduct.Images.Add(new ProductImage { ImagePath = $"https://placehold.co/600x600/{random.Next(100000, 999999)}/ffffff?text={Uri.EscapeDataString(itemName)}", SortOrder = 0 });
           genProduct.Variants.Add(new ProductVariant { Stock = random.Next(10, 100), Price = price, Sku = genProduct.Slug, Color = "Standard" });
           products.Add(genProduct);
        }

        return products;
    }

    private List<Product> CreateDailyMartProducts(List<Category> categories, Seller seller)
    {
        var products = new List<Product>();
        var random = new Random();


        // Helper to find category or default
        Category GetCategory(string slug) => categories.FirstOrDefault(c => c.Slug == slug) ?? categories.First();

        var appliance = GetCategory("home-appliances");
        var electronics = GetCategory("electronics-gadgets");
        var household = GetCategory("home-living");
        var grocery = GetCategory("groceries-essentials");
        var kitchen = GetCategory("kitchen-small-appliances");

        // 1. Walton Smart LED Bulb
        var p1 = new Product
        {
            Title = "Walton Smart LED Bulb WBL-12W",
            Slug = "walton-smart-led-bulb-wbl-12w",
            ShortDescription = "12W smart LED WiFi bulb",
            Description = "12W smart LED WiFi bulb offering remote control via Walton Smart App. Adjustable brightness, warm/cool tones, scheduling, and energy-efficient. Perfect for living rooms, bedrooms, and offices. Material: Plastic + Electronic Components. Warranty: 1 Year Replacement Warranty.",
            BasePrice = 650,
            CategoryId = appliance.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.22 kg",
            Dimensions = "12 x 6 x 6 cm",
            Features = "12W high-efficiency LED|Warm & Cool color modes|WiFi app control|Scheduling & timers|20,000+ hours lifespan",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/fcd34d/000000?text=Walton+Smart+LED", SortOrder = 0 });
        p1.Variants.Add(new ProductVariant { Stock = 120, Price = 650, Sku = "WALT-SMART-LED-12W", Color = "Standard" });
        products.Add(p1);

        // 2. Vision Rechargeable Fan
        var p2 = new Product
        {
            Title = "Vision Rechargeable Fan VRF-138",
            Slug = "vision-rechargeable-fan-vrf-138",
            ShortDescription = "16-inch rechargeable fan, 5hr backup",
            Description = "16-inch rechargeable fan with up to 5 hours backup. 3-speed control, LED night lamp, low noise. Designed for homes, offices, and areas with frequent power outages. Material: ABS Plastic + Metal Motor. Warranty: 6 Months Warranty.",
            BasePrice = 3150,
            CategoryId = electronics.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "3.4 kg",
            Dimensions = "48 x 20 x 48 cm",
            Features = "Rechargeable battery up to 5 hours|3-speed airflow control|Built-in LED night light|Low noise operation|Durable ABS plastic body",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p2.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/3b82f6/ffffff?text=Vision+Rechargeable+Fan", SortOrder = 0 });
        p2.Variants.Add(new ProductVariant { Stock = 85, Price = 3150, Sku = "VIS-RECH-FAN-138", Color = "Blue" });
        products.Add(p2);

        // 3. RFL Jumbo Laundry Basket
        var p3 = new Product
        {
            Title = "RFL Jumbo Laundry Basket 45L",
            Slug = "rfl-jumbo-laundry-basket-45l",
            ShortDescription = "45L ventilated laundry basket",
            Description = "Premium 45L laundry basket with ventilation holes to prevent odor. Lightweight, durable, easy to carry, and perfect for family laundry storage. Material: High-Density Plastic. Warranty: No Warranty.",
            BasePrice = 480,
            CategoryId = household.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.6 kg",
            Dimensions = "45 x 38 x 32 cm",
            Features = "Large 45L capacity|Ventilated design|Sturdy and flexible plastic|Side grip handles|Water-resistant",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p3.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/fb923c/ffffff?text=RFL+Laundry+Basket", SortOrder = 0 });
        p3.Variants.Add(new ProductVariant { Stock = 200, Price = 480, Sku = "RFL-LAUNDRY-45L", Color = "Orange" });
        products.Add(p3);

        // 4. Singer Quartz Room Heater
        var p4 = new Product
        {
            Title = "Singer Quartz Room Heater SHQ1201",
            Slug = "singer-quartz-room-heater-shq1201",
            ShortDescription = "Compact quartz room heater",
            Description = "Compact quartz room heater with two heat modes, instant warmth, and built-in safety cut-off. Energy-efficient and ideal for winter in bedrooms or small living rooms. Material: Metal + Plastic Body. Warranty: 1 Year Warranty.",
            BasePrice = 2100,
            CategoryId = appliance.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "1.8 kg",
            Dimensions = "38 x 25 x 8 cm",
            Features = "Dual heat settings|Low power consumption|Safety cut-off feature|Portable design",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p4.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/ef4444/ffffff?text=Singer+Room+Heater", SortOrder = 0 });
        p4.Variants.Add(new ProductVariant { Stock = 60, Price = 2100, Sku = "SING-HEAT-SHQ1201", Color = "White" });
        products.Add(p4);

        // 5. Bashundhara Kitchen Tissue
        var p5 = new Product
        {
            Title = "Bashundhara Kitchen Tissue Premium 2-Ply",
            Slug = "bashundhara-kitchen-tissue-premium-2-ply",
            ShortDescription = "High-quality 2-ply kitchen tissue",
            Description = "High-quality 2-ply kitchen tissue roll. Excellent absorbency, hygienic, and safe for food contact. Ideal for cleaning spills, kitchen hygiene, and general household use. Material: Soft Tissue Paper. Warranty: No Warranty.",
            BasePrice = 120,
            CategoryId = grocery.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.18 kg",
            Dimensions = "10 x 10 x 24 cm",
            Features = "2-ply thickness|High absorbency|Tear-resistant|Food-grade material|Eco-friendly manufacturing",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p5.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/f3f4f6/000000?text=Bashundhara+Tissue", SortOrder = 0 });
        p5.Variants.Add(new ProductVariant { Stock = 500, Price = 120, Sku = "BASH-TISSUE-2PLY", Color = "White" });
        products.Add(p5);

        // 6. RFL Supreme Water Bottle
        var p6 = new Product
        {
            Title = "RFL Supreme Water Bottle 1.2L",
            Slug = "rfl-supreme-water-bottle-1-2l",
            ShortDescription = "1.2L BPA-free water bottle",
            Description = "1.2-liter BPA-free water bottle with leak-proof cap and durable design. Portable, easy to clean, ideal for school, gym, office, and home. Material: BPA-Free Plastic. Warranty: No Warranty.",
            BasePrice = 140,
            CategoryId = household.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.26 kg",
            Dimensions = "28 x 9 x 9 cm",
            Features = "Leak-proof screw-cap|BPA-free safe material|Lightweight and portable|Durable body|Easy to clean",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p6.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/3b82f6/ffffff?text=RFL+Water+Bottle", SortOrder = 0 });
        p6.Variants.Add(new ProductVariant { Stock = 300, Price = 140, Sku = "RFL-SUP-1.2L", Color = "Blue" });
        products.Add(p6);

        // 7. Walton Electric Kettle
        var p7 = new Product
        {
            Title = "Walton Electric Kettle WEK-K17L",
            Slug = "walton-electric-kettle-wek-k17l",
            ShortDescription = "1.7L stainless steel kettle",
            Description = "1.7L stainless steel electric kettle with auto shut-off and overheating protection. Fast boiling and ergonomic handle for safe and easy pouring. Material: Stainless Steel Body. Warranty: 1 Year Warranty.",
            BasePrice = 1250,
            CategoryId = kitchen.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.9 kg",
            Dimensions = "22 x 16 x 16 cm",
            Features = "1.7L large capacity|Auto shut-off|Fast boiling 1500W|Stainless steel body|Cool-touch handle",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p7.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/d1d5db/000000?text=Walton+Electric+Kettle", SortOrder = 0 });
        p7.Variants.Add(new ProductVariant { Stock = 110, Price = 1250, Sku = "WALT-KET-K17L", Color = "Silver" });
        products.Add(p7);

        // 8. Miyako 3-in-1 Blender
        var p8 = new Product
        {
            Title = "Miyako 3-in-1 Blender BL-152",
            Slug = "miyako-3-in-1-blender-bl-152",
            ShortDescription = "400W blender, grinder, juicer",
            Description = "Multifunctional 3-in-1 blender, grinder, and juicer. 400W motor, sharp stainless steel blades, durable jars for vegetables, spices, and juices. Material: Plastic + Metal Blades. Warranty: 1 Year Service Warranty.",
            BasePrice = 2200,
            CategoryId = kitchen.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "2.2 kg",
            Dimensions = "36 x 20 x 19 cm",
            Features = "Blender + Grinder + Juicer|400W motor|Stainless steel blades|Multiple jar sizes|Shock-resistant base",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p8.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/ef4444/ffffff?text=Miyako+Blender", SortOrder = 0 });
        p8.Variants.Add(new ProductVariant { Stock = 90, Price = 2200, Sku = "MIYAKO-BL-152", Color = "Red" });
        products.Add(p8);

        // 9. Radhuni Turmeric Powder
        var p9 = new Product
        {
            Title = "Radhuni Turmeric Powder 200g",
            Slug = "radhuni-turmeric-powder-200g",
            ShortDescription = "Premium quality turmeric powder",
            Description = "High-quality turmeric powder, essential for Bangladeshi cooking. Strong color and aroma for curries, lentils, and traditional dishes. Material: Spice Powder. Warranty: No Warranty.",
            BasePrice = 85,
            CategoryId = grocery.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.2 kg",
            Dimensions = "10 x 10 x 5 cm",
            Features = "Fresh aroma|Vibrant color|100% pure spice|Rich flavor|Suitable for daily use",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p9.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/fbbf24/000000?text=Radhuni+Turmeric", SortOrder = 0 });
        p9.Variants.Add(new ProductVariant { Stock = 450, Price = 85, Sku = "RAD-TURM-200G", Color = "Yellow" });
        products.Add(p9);

        // 10. Pran UHT Milk
        var p10 = new Product
        {
            Title = "Pran UHT Milk 1L",
            Slug = "pran-uht-milk-1l",
            ShortDescription = "1L UHT fresh milk",
            Description = "Long-life ultra-heat-treated fresh milk with rich taste. Suitable for tea, coffee, cereal, or direct consumption. Convenient 1-liter pack. Material: Tetrapak. Warranty: No Warranty.",
            BasePrice = 95,
            CategoryId = grocery.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "1.05 kg",
            Dimensions = "25 x 8 x 8 cm",
            Features = "UHT processed|Rich taste|Long shelf life|Convenient packaging",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p10.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/ffffff/000000?text=Pran+Milk", SortOrder = 0 });
        p10.Variants.Add(new ProductVariant { Stock = 400, Price = 95, Sku = "PRAN-MILK-1L", Color = "White" });
        products.Add(p10);

        // --- Generated ~20 additional products for DailyMart BD ---
        
        string[] groceryItems = { "Teer Soybean Oil 5L", "ACI Pure Salt 1kg", "Sugor Refined Sugar 1kg", "Radhuni Chili Powder 200g", "Ispahani Mirzapore Tea 400g", "Pran Toast Biscuits 350g", "Maggi Noodles 8 Pack", "Dettol Hand Wash 200ml", "Harpic Toilet Cleaner 750ml", "Vim Dishwash Bar 300g", "Wheel Laundry Soap 130g", "Parachute Coconut Oil 200ml" };
        string[] householdItems = { "RFL Plastic Chair Armless", "RFL Mop Set Premium", "RFL Broom Plastic", "RFL Shoe Rack 4-Step", "RFL Waste Bin Pedal 20L", "RFL Bucket 10L Red", "RFL Storage Box 30L", "Aluminum Kড়াই 24cm" };

        for (int i = 0; i < 20; i++)
        {
           string itemName;
           Category itemCat;
           decimal price;
           
           if (i < 12) {
              itemName = groceryItems[i % groceryItems.Length];
              itemCat = grocery;
              price = random.Next(40, 900);
           } else {
              itemName = householdItems[i % householdItems.Length];
              itemCat = household;
              price = random.Next(150, 1500);
           }

           var genProduct = new Product
           {
               Title = itemName,
               Slug = itemName.ToLower().Replace(" ", "-").Replace("\"", "") + $"-{random.Next(1000,9999)}",
               ShortDescription = $"Daily essential {itemName}",
               Description = $"{itemName} is a must-have for your daily needs. Sourced from trusted brands and guaranteed quality. Material: Various. Warranty: No Warranty.",
               BasePrice = price,
               CategoryId = itemCat.Id,
               SellerId = seller.Id,
               IsActive = true,
               Weight = $"{random.Next(1, 100) / 10.0} kg",
               Dimensions = "Standard Size",
               Features = "Daily Essential|Premium Quality|Best Price",
               CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 60))
           };
           
           genProduct.Images.Add(new ProductImage { ImagePath = $"https://placehold.co/600x600/{random.Next(100000, 999999)}/ffffff?text={Uri.EscapeDataString(itemName)}", SortOrder = 0 });
           genProduct.Variants.Add(new ProductVariant { Stock = random.Next(20, 500), Price = price, Sku = genProduct.Slug, Color = "Standard" });
           products.Add(genProduct);
        }

        return products;
    }


    private List<Product> CreateHomeNeedsProducts(List<Category> categories, Seller seller)
    {
        var products = new List<Product>();
        var random = new Random();


        // Helper to find category or default
        Category GetCategory(string slug) => categories.FirstOrDefault(c => c.Slug == slug) ?? categories.First();

        var electronics = GetCategory("electronics-gadgets");
        var appliance = GetCategory("home-appliances");
        var household = GetCategory("home-living");
        var kitchen = GetCategory("kitchen-small-appliances");
        var personalCare = GetCategory("beauty-personal-care");
        var fashionMen = GetCategory("fashion-lifestyle");

        // 1. Walton Rechargeable Table Fan WRTF18
        var p1 = new Product
        {
            Title = "Walton Rechargeable Table Fan WRTF18",
            Slug = "walton-rechargeable-table-fan-wrtf18",
            ShortDescription = "18-inch rechargeable fan, 4-5hr backup",
            Description = "18-inch rechargeable table fan with high-speed airflow and battery backup for 4-5 hours. Compact, portable, and ideal for load-shedding areas. Material: ABS Plastic + Metal Motor. Warranty: 6 Months Warranty.",
            BasePrice = 3750,
            CategoryId = electronics.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "3.2 kg",
            Dimensions = "45 x 20 x 45 cm",
            Features = "Rechargeable battery up to 5 hours|3-speed control|Quiet operation|Portable and lightweight",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p1.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/3b82f6/ffffff?text=Walton+TF18+Fan", SortOrder = 0 });
        p1.Variants.Add(new ProductVariant { Stock = 90, Price = 3750, Sku = "WALT-FAN-TF18", Color = "White" });
        products.Add(p1);

        // 2. Vision LED Bulb 9W Cool White
        var p2 = new Product
        {
            Title = "Vision LED Bulb 9W Cool White",
            Slug = "vision-led-bulb-9w-cool-white",
            ShortDescription = "9W energy-efficient cool white bulb",
            Description = "9W energy-efficient LED bulb emitting cool white light. Perfect for study rooms, kitchens, and offices with low power consumption. Material: Plastic + Electronics. Warranty: 1 Year Warranty.",
            BasePrice = 180,
            CategoryId = appliance.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.12 kg",
            Dimensions = "10 x 5 x 5 cm",
            Features = "9W low-power consumption|Long lifespan 15,000+ hours|Cool white illumination|Easy installation",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p2.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/f3f4f6/000000?text=Vision+9W+LED", SortOrder = 0 });
        p2.Variants.Add(new ProductVariant { Stock = 200, Price = 180, Sku = "VIS-LED-9W", Color = "White" });
        products.Add(p2);

        // 3. RFL Water Jug Crystal 3L
        var p3 = new Product
        {
            Title = "RFL Water Jug Crystal 3L",
            Slug = "rfl-water-jug-crystal-3l",
            ShortDescription = "3L BPA-free water jug",
            Description = "3-liter BPA-free water jug for daily hydration. Lightweight, durable, and easy to clean. Suitable for kitchen and dining areas. Material: BPA-Free Plastic. Warranty: No Warranty.",
            BasePrice = 210,
            CategoryId = household.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.45 kg",
            Dimensions = "28 x 16 x 22 cm",
            Features = "BPA-free material|Lightweight and portable|Durable construction|Easy to clean",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p3.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/06b6d4/ffffff?text=RFL+Jug+3L", SortOrder = 0 });
        p3.Variants.Add(new ProductVariant { Stock = 180, Price = 210, Sku = "RFL-JUG-3L", Color = "Blue" });
        products.Add(p3);

        // 4. Kiam Stainless Steel Mixing Bowl Set
        var p4 = new Product
        {
            Title = "Kiam Stainless Steel Mixing Bowl Set 4pcs",
            Slug = "kiam-stainless-steel-mixing-bowl-set-4pcs",
            ShortDescription = "4pcs rust-resistant mixing bowls",
            Description = "Set of 4 stainless steel mixing bowls of varying sizes. Rust-resistant, durable, and ideal for food prep, mixing, and serving in daily cooking. Material: Stainless Steel. Warranty: No Warranty.",
            BasePrice = 650,
            CategoryId = kitchen.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "1.8 kg",
            Dimensions = "25 x 20 x 15 cm",
            Features = "Rust-resistant stainless steel|4 different sizes|Durable and sturdy|Easy to clean",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p4.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/9ca3af/ffffff?text=Kiam+Bowl+Set", SortOrder = 0 });
        p4.Variants.Add(new ProductVariant { Stock = 130, Price = 650, Sku = "KIAM-BOWL-4SET", Color = "Silver" });
        products.Add(p4);

        // 5. Walton Electric Kettle WEK-K18
        var p5 = new Product
        {
            Title = "Walton Electric Kettle WEK-K18",
            Slug = "walton-electric-kettle-wek-k18",
            ShortDescription = "1.8L fast-boiling electric kettle",
            Description = "1.8L electric kettle with auto shut-off and overheating protection. Boils water quickly and safely, suitable for daily use in homes and offices. Material: Stainless Steel + Plastic Handle. Warranty: 1 Year Warranty.",
            BasePrice = 1350,
            CategoryId = kitchen.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "1.0 kg",
            Dimensions = "23 x 16 x 16 cm",
            Features = "1.8L capacity|Auto shut-off safety feature|Fast boiling|Ergonomic handle|Stainless steel body",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p5.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/d1d5db/000000?text=Walton+Kettle+K18", SortOrder = 0 });
        p5.Variants.Add(new ProductVariant { Stock = 110, Price = 1350, Sku = "WALT-KET-K18", Color = "Silver" });
        products.Add(p5);

        // 6. Dettol Anti-Bacterial Soap
        var p6 = new Product
        {
            Title = "Dettol Anti-Bacterial Soap 100g x4",
            Slug = "dettol-anti-bacterial-soap-100g-x4",
            ShortDescription = "Pack of 4 anti-bacterial soaps",
            Description = "Pack of 4 anti-bacterial soap bars. Effective for hand and body cleansing, daily use, and maintaining hygiene in households. Material: Soap Bar. Warranty: No Warranty.",
            BasePrice = 170,
            CategoryId = personalCare.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.4 kg",
            Dimensions = "10 x 6 x 5 cm",
            Features = "Kills 99.9% germs|Moisturizing formula|Suitable for daily use|Refreshing fragrance",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p6.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/10b981/ffffff?text=Dettol+Soap", SortOrder = 0 });
        p6.Variants.Add(new ProductVariant { Stock = 250, Price = 170, Sku = "DET-SOAP-100G", Color = "Green" });
        products.Add(p6);

        // 7. Pepsodent Germicheck Toothpaste
        var p7 = new Product
        {
            Title = "Pepsodent Germicheck Toothpaste 200g",
            Slug = "pepsodent-germicheck-toothpaste-200g",
            ShortDescription = "Anti-bacterial toothpaste for oral hygiene",
            Description = "Anti-bacterial toothpaste to maintain oral hygiene and protect against cavities. Suitable for daily brushing in Bangladeshi households. Material: Toothpaste. Warranty: No Warranty.",
            BasePrice = 120,
            CategoryId = personalCare.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.25 kg",
            Dimensions = "18 x 5 x 3 cm",
            Features = "Anti-bacterial protection|Strengthens teeth|Freshens breath|Daily oral hygiene",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p7.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/ef4444/ffffff?text=Pepsodent", SortOrder = 0 });
        p7.Variants.Add(new ProductVariant { Stock = 300, Price = 120, Sku = "PEP-TOOTH-200G", Color = "White" });
        products.Add(p7);

        // 8. Himalaya Neem Face Wash
        var p8 = new Product
        {
            Title = "Himalaya Neem Face Wash 150ml",
            Slug = "himalaya-neem-face-wash-150ml",
            ShortDescription = "Herbal face wash with neem extracts",
            Description = "Herbal face wash enriched with neem extracts. Controls acne, removes dirt and oil, suitable for daily facial cleansing in hot and humid climates. Material: Herbal Gel. Warranty: No Warranty.",
            BasePrice = 230,
            CategoryId = personalCare.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.18 kg",
            Dimensions = "20 x 6 x 5 cm",
            Features = "Herbal formula|Removes impurities|Controls acne|Gentle on skin",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p8.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/84cc16/ffffff?text=Himalaya+Face+Wash", SortOrder = 0 });
        p8.Variants.Add(new ProductVariant { Stock = 200, Price = 230, Sku = "HIM-FACE-150ML", Color = "Green" });
        products.Add(p8);

        // 9. Aarong Cotton Panjabi
        var p9 = new Product
        {
            Title = "Aarong Cotton Panjabi Classic White",
            Slug = "aarong-cotton-panjabi-classic-white",
            ShortDescription = "100% cotton premium white Panjabi",
            Description = "Premium cotton Panjabi suitable for daily wear, casual outings, and traditional events. Soft, breathable, and comfortable for Bangladeshi weather. Material: 100% Cotton. Warranty: No Warranty.",
            BasePrice = 2200,
            CategoryId = fashionMen.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.35 kg",
            Dimensions = "Variable sizes",
            Features = "100% pure cotton|Lightweight and breathable|Durable stitching|Traditional design",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p9.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/f8fafc/000000?text=Aarong+Panjabi", SortOrder = 0 });
        p9.Variants.Add(new ProductVariant { Stock = 70, Price = 2200, Sku = "AAR-PANJ-WHT", Color = "White" });
        products.Add(p9);

        // 10. Bata Men’s Casual Shoes
        var p10 = new Product
        {
            Title = "Bata Men’s Casual Shoes 881-2321",
            Slug = "bata-mens-casual-shoes-881-2321",
            ShortDescription = "Comfortable synthetic leather casual shoes",
            Description = "Lightweight, comfortable casual shoes suitable for daily office wear or outings. Durable rubber sole ensures strong grip and long-lasting usage. Material: Synthetic Leather + Rubber Sole. Warranty: 3 Months Manufacturer Warranty.",
            BasePrice = 2490,
            CategoryId = fashionMen.Id,
            SellerId = seller.Id,
            IsActive = true,
            Weight = "0.8 kg",
            Dimensions = "Size dependent",
            Features = "Durable synthetic leather|Comfortable cushioning|Non-slip rubber sole|Lightweight design",
            CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 15))
        };
        p10.Images.Add(new ProductImage { ImagePath = "https://placehold.co/600x600/475569/ffffff?text=Bata+Shoes", SortOrder = 0 });
        p10.Variants.Add(new ProductVariant { Stock = 60, Price = 2490, Sku = "BATA-SHOE-881", Color = "Brown" });
        products.Add(p10);

        // --- Generated ~20 additional products for HomeNeeds BD ---
        
        string[] elecItems = { "Walton 43\" Smart TV", "Vision Blender 850W", "Miyako Curry Cooker", "Philips Hair Dryer 1000W", "Singer Irons Steam", "Trimmer Phillips Series 3000" };
        string[] householdItems = { "RFL Doormat Premium", "RFL Dining Chair Classic", "RFL Wall Hanger 6-Hook", "RFL Shoe Rack 5-Step", "RFL Laundry Basket 30L", "RFL Kitchen Rack 4-Tier" };
        string[] personalItems = { "Lux Soap 100g", "Sunsilk Shampoo 180ml", "Parachute body Lotion", "Pond's Face Wash", "Closeup Toothpaste Red", "Savlon Antiseptic 100ml" };

        for (int i = 0; i < 20; i++)
        {
           string itemName;
           Category itemCat;
           decimal price;
           
           if (i < 6) {
              itemName = elecItems[i % elecItems.Length];
              itemCat = (itemName.Contains("TV") || itemName.Contains("Trimmer") || itemName.Contains("Hair")) ? electronics : appliance;
              price = random.Next(1000, 25000);
           } else if (i < 12) {
              itemName = householdItems[i % householdItems.Length];
              itemCat = household;
              price = random.Next(100, 2000);
           } else {
              itemName = personalItems[i % personalItems.Length];
              itemCat = personalCare;
              price = random.Next(50, 400);
           }

           var genProduct = new Product
           {
               Title = itemName,
               Slug = itemName.ToLower().Replace(" ", "-").Replace("\"", "") + $"-{random.Next(1000,9999)}",
               ShortDescription = $"Home essential {itemName}",
               Description = $"{itemName} brings convenience and quality to your home. Trusted brand product ensuring satisfaction. Material: Various. Warranty: { (price > 1000 ? "1 Year Warranty" : "No Warranty") }.",
               BasePrice = price,
               CategoryId = itemCat.Id,
               SellerId = seller.Id,
               IsActive = true,
               Weight = $"{random.Next(1, 50) / 10.0} kg",
               Dimensions = "Standard",
               Features = "Quality Product|Durable|Affordable",
               CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 60))
           };
           
           genProduct.Images.Add(new ProductImage { ImagePath = $"https://placehold.co/600x600/{random.Next(100000, 999999)}/ffffff?text={Uri.EscapeDataString(itemName)}", SortOrder = 0 });
           genProduct.Variants.Add(new ProductVariant { Stock = random.Next(10, 200), Price = price, Sku = genProduct.Slug, Color = "Standard" });
           products.Add(genProduct);
        }

        return products;
    }
    private async Task EnsureFlashSaleProductsAsync()
    {
        try
        {
            // Optimized check: stop counting if we have 24 items
            var flashSaleCount = await _db.Products
                .Where(p => p.DiscountPercent > 15)
                .OrderBy(p => p.Id)
                .Take(24)
                .CountAsync();
                
            if (flashSaleCount < 24)
            {
                _logger.LogInformation($"Found only {flashSaleCount} Flash Sale items. Upgrading more products...");
                
                var candidates = await _db.Products
                    .Where(p => p.DiscountPercent <= 15)
                    .OrderBy(x => Guid.NewGuid())
                    .Take(24) 
                    .ToListAsync();

                var random = new Random();
                foreach (var prod in candidates)
                {
                    prod.DiscountPercent = random.Next(20, 50);
                }
                
                if (candidates.Any())
                {
                    await _db.SaveChangesAsync();
                    _logger.LogInformation($"Upgraded {candidates.Count} products to Flash Sale status.");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ensuring Flash Sale products.");
        }
    }

    private async Task EnsurePerformanceIndexesAsync()
    {
        try 
        {
            _logger.LogInformation("Verifying and repairing database schema...");
            
            // 1. Schema repairs from DbFixController
            var repairs = new[]
            {
                // Fix Column Types for Indexing (must be done before creating indexes)
                "IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[orders].[Carts]') AND name = 'UserId' AND max_length = -1) ALTER TABLE [orders].[Carts] ALTER COLUMN [UserId] nvarchar(450) NOT NULL;",
                "IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[catalog].[Categories]') AND name = 'Slug' AND max_length = -1) ALTER TABLE [catalog].[Categories] ALTER COLUMN [Slug] nvarchar(150) NOT NULL;",
                "IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[catalog].[Products]') AND name = 'Slug' AND max_length = -1) ALTER TABLE [catalog].[Products] ALTER COLUMN [Slug] nvarchar(150) NOT NULL;",
                "IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND name = 'Name' AND max_length = -1) ALTER TABLE [content].[HomepageSections] ALTER COLUMN [Name] nvarchar(255) NOT NULL;",
                "IF EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[content].[HomepageSections]') AND name = 'Slug' AND max_length = -1) ALTER TABLE [content].[HomepageSections] ALTER COLUMN [Slug] nvarchar(150) NOT NULL;",

                // Shipments columns
                "IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[shipping].[Shipments]') AND name = 'NumberOfBoxes') ALTER TABLE [shipping].[Shipments] ADD [NumberOfBoxes] int NOT NULL DEFAULT 1;",
                "IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[shipping].[Shipments]') AND name = 'PackageType') ALTER TABLE [shipping].[Shipments] ADD [PackageType] nvarchar(max) NULL;",
                "IF NOT EXISTS(SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[shipping].[Shipments]') AND name = 'ShippedAt') ALTER TABLE [shipping].[Shipments] ADD [ShippedAt] datetime2 NULL;",
                
                // Wallets schema and tables
                "IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'wallets') EXEC('CREATE SCHEMA [wallets]');",
                @"IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[wallets].[SellerWallets]') AND type in (N'U'))
                  BEGIN
                      CREATE TABLE [wallets].[SellerWallets] (
                          [Id] int NOT NULL IDENTITY,
                          [SellerId] int NOT NULL,
                          [AvailableBalance] decimal(18,2) NOT NULL,
                          [PendingBalance] decimal(18,2) NOT NULL,
                          [TotalEarnings] decimal(18,2) NOT NULL,
                          [TotalWithdrawn] decimal(18,2) NOT NULL,
                          [Currency] nvarchar(max) NOT NULL,
                          [IsActive] bit NOT NULL,
                          [CreatedAt] datetime2 NOT NULL,
                          [UpdatedAt] datetime2 NULL,
                          [CreatedBy] nvarchar(max) NULL,
                          [UpdatedBy] nvarchar(max) NULL,
                          [IsDeleted] bit NOT NULL,
                          [DeletedAt] datetime2 NULL,
                          CONSTRAINT [PK_SellerWallets] PRIMARY KEY ([Id]),
                          CONSTRAINT [FK_SellerWallets_Sellers_SellerId] FOREIGN KEY ([SellerId]) REFERENCES [sellers].[Sellers] ([Id]) ON DELETE CASCADE
                      );
                  END"
            };

            foreach (var sql in repairs)
            {
                await _db.Database.ExecuteSqlRawAsync(sql);
            }

            _logger.LogInformation("Verifying and creating performance indexes...");
            
            var indexes = new[]
            {
                ("IX_Carts_UserId", "orders.Carts", "UserId"),
                ("IX_Categories_Slug", "catalog.Categories", "Slug"),
                ("IX_Products_Slug", "catalog.Products", "Slug"),
                ("IX_Products_DiscountPercent", "catalog.Products", "DiscountPercent"),
                ("IX_Products_IsActive", "catalog.Products", "IsActive"),
                ("IX_Products_IsAdminProduct", "catalog.Products", "IsAdminProduct"),
                ("IX_HomepageSections_Name", "content.HomepageSections", "Name"),
                // Critical composite index for homepage performance
                ("IX_Products_Active_Perf_Comp", "catalog.Products", "IsActive, TotalReviews DESC, AverageRating DESC")
            };

            foreach (var (indexName, tableName, columnName) in indexes)
            {
                // Ensure columns with spaces (sort directions) or commas (composite) are handled correctly
                // We'll strip any existing brackets and apply them per-column if we wanted to be fancy, 
                // but for now, passing the exact column string is more flexible for sort orders.
                var formattedTable = tableName.Contains(".") ? tableName.Replace(".", "].[") : "dbo].[" + tableName;
                var sql = $@"
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = '{indexName}' AND object_id = OBJECT_ID(N'[{formattedTable}]'))
                    BEGIN
                        CREATE INDEX [{indexName}] ON [{formattedTable}] ({columnName});
                    END";

                await _db.Database.ExecuteSqlRawAsync(sql);
            }
            
            _logger.LogInformation("Database initialization and performance indexing completed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during database initialization/optimization.");
        }
    }
}
