using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Content;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Services;

public class HomepageSectionSeedingService
{
    private readonly ApplicationDbContext _db;

    public HomepageSectionSeedingService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        // Cleanup obsolete/duplicate sections
        var obsoleteNames = new[] { "Shop by Category", "Trending Products", "ট্রেন্ডিং পণ্য" };
        var obsoleteSections = await _db.HomepageSections
            .Where(s => obsoleteNames.Contains(s.Name))
            .ToListAsync();
        
        if (obsoleteSections.Any())
        {
            _db.HomepageSections.RemoveRange(obsoleteSections);
            await _db.SaveChangesAsync();
        }

        var sections = new List<HomepageSection>
        {
            new HomepageSection
            {
                Name = "Flash Sale",
                DisplayTitle = "⚡ Incredible Deals",
                SectionType = "FlashSale",
                DisplayOrder = 1,
                IsActive = true,
                UseAutomatedSelection = true,
                LayoutType = "Carousel",
                MaxProductsToDisplay = 12,
                CardSize = "Medium",
                ProductsPerRow = 5,
                ShowPrice = true,
                ShowRating = true,
                ShowDiscount = true,
                Slug = "flash-sale",
                UpdatedAt = DateTime.UtcNow
            },
            new HomepageSection
            {
                Name = "Trending Now",
                DisplayTitle = "🔥 Trending Now",
                SectionType = "TrendingProducts",
                DisplayOrder = 2,
                IsActive = true,
                UseAutomatedSelection = true,
                LayoutType = "Grid",
                MaxProductsToDisplay = 10,
                CardSize = "Medium",
                ProductsPerRow = 5,
                ShowPrice = true,
                ShowRating = true,
                ShowDiscount = true,
                Slug = "trending-now",
                UpdatedAt = DateTime.UtcNow
            },
            new HomepageSection
            {
                Name = "Recommended for You",
                DisplayTitle = "✨ Just For You",
                SectionType = "RecommendedProducts",
                DisplayOrder = 3,
                IsActive = true,
                UseAutomatedSelection = true,
                LayoutType = "Grid",
                MaxProductsToDisplay = 15,
                CardSize = "Medium",
                ProductsPerRow = 5,
                ShowPrice = true,
                ShowRating = true,
                ShowDiscount = true,
                Slug = "recommended-products",
                UpdatedAt = DateTime.UtcNow
            }
        };

        foreach (var section in sections)
        {
            var existing = await _db.HomepageSections.FirstOrDefaultAsync(s => s.Slug == section.Slug || s.Name == section.Name);
            if (existing == null)
            {
                _db.HomepageSections.Add(section);
            }
            else
            {
                // Update existing section settings to match new configuration
                existing.Name = section.Name; // Ensure name is synced if it was found by slug
                existing.Slug = section.Slug; // Ensure slug is synced if it was found by name
                existing.DisplayTitle = section.DisplayTitle;
                existing.MaxProductsToDisplay = section.MaxProductsToDisplay;
                existing.LayoutType = section.LayoutType;
                existing.ProductsPerRow = section.ProductsPerRow;
                existing.UpdatedAt = DateTime.UtcNow;
                _db.Entry(existing).State = EntityState.Modified;
            }
        }
        await _db.SaveChangesAsync();
    }
}
