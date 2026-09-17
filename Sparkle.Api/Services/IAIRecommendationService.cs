using Sparkle.Domain.Catalog;
using Sparkle.Domain.Content;

namespace Sparkle.Api.Services;

public interface IAIRecommendationService
{
    Task<List<Product>> GetTrendingProductsAsync(int count);
    Task<List<Product>> GetRecommendedProductsAsync(string? userId, int count);
    Task<List<Product>> GetFlashSaleSuggestionsAsync(int count);
    Task AnalyzeUserBehaviorAsync(string? userId, int? productId, string actionType);
    Task<List<Product>> GetProductsForSectionAsync(HomepageSection section);
}
