using Microsoft.EntityFrameworkCore;

namespace Sparkle.Api.Services;

public class AIRecommendationService : IAIRecommendationService
{
    private readonly Sparkle.Infrastructure.ApplicationDbContext _db;
    private readonly IIntelligentProductAnalysisService _analysisService;

    public AIRecommendationService(Sparkle.Infrastructure.ApplicationDbContext db, IIntelligentProductAnalysisService analysisService)
    {
        _db = db;
        _analysisService = analysisService;
    }

    public async Task<List<Sparkle.Domain.Catalog.Product>> GetTrendingProductsAsync(int count)
    {
        return await _analysisService.GetCurrentTrendingProductsAsync(count);
    }

    public async Task<List<Sparkle.Domain.Catalog.Product>> GetRecommendedProductsAsync(string? userId, int count)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return await GetTrendingProductsAsync(count);
        }

        var productIds = await _analysisService.GetRecommendedProductsForUserAsync(userId, count);
        return await _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Where(p => p.IsActive && productIds.Contains(p.Id))
            .ToListAsync();
    }

    public async Task<List<Sparkle.Domain.Catalog.Product>> GetFlashSaleSuggestionsAsync(int count)
    {
        return await _analysisService.GetSuggestedFlashSaleProductsAsync(count);
    }

    public async Task AnalyzeUserBehaviorAsync(string? userId, int? productId, string actionType)
    {
        if (string.IsNullOrEmpty(userId)) return;
        await _analysisService.LogUserActionAsync(userId, actionType, productId);
    }

    public async Task<List<Sparkle.Domain.Catalog.Product>> GetProductsForSectionAsync(Sparkle.Domain.Content.HomepageSection section)
    {
        List<Sparkle.Domain.Catalog.Product> products;
        if (section.UseAutomatedSelection)
        {
            var count = section.MaxProductsToDisplay;
            products = section.SectionType switch
            {
                "TrendingProducts" => await GetTrendingProductsAsync(count),
                "FlashSale" => await GetFlashSaleSuggestionsAsync(count),
                "RecommendedProducts" => await GetRecommendedProductsAsync(null, count), 
                "CategoryShop" => await GetCategoryProductsAsync(section, count),
                _ => await GetTrendingProductsAsync(count)
            };

            // Fallback: If no intelligent products found (common in fresh DB), return random active products
            if (products == null || !products.Any())
            {
                products = await _db.Products
                    .AsNoTracking()
                    .Include(p => p.Images)
                    .Include(p => p.Variants)
                    .Where(p => p.IsActive)
                    .OrderBy(p => Guid.NewGuid()) // Random for variety
                    .Take(count)
                    .ToListAsync();
            }

            return products;
        }

        // Manual selection
        return await _db.HomepageSectionProducts
            .AsNoTracking()
            .Include(sp => sp.Product)
            .ThenInclude(p => p.Images)
            .Include(sp => sp.Product)
            .ThenInclude(p => p.Variants)
            .Where(sp => sp.SectionId == section.Id && sp.IsActive)
            .OrderBy(sp => sp.DisplayOrder)
            .Select(sp => sp.Product)
            .Take(section.MaxProductsToDisplay)
            .ToListAsync();
    }

    private async Task<List<Sparkle.Domain.Catalog.Product>> GetCategoryProductsAsync(Sparkle.Domain.Content.HomepageSection section, int count)
    {
        // For category shop, we look at the RelatedCategories linked to this section
        var categoryIds = await _db.HomepageSectionCategories
            .AsNoTracking()
            .Where(rc => rc.SectionId == section.Id && rc.IsActive)
            .Select(rc => rc.CategoryId)
            .ToListAsync();

        if (!categoryIds.Any())
        {
            return new List<Sparkle.Domain.Catalog.Product>();
        }

        return await _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => p.IsActive && categoryIds.Contains(p.CategoryId))
            .OrderByDescending(p => p.AverageRating)
            .Take(count)
            .ToListAsync();
    }
}
