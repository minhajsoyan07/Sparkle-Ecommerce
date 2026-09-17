using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sparkle.Domain.Intelligence;

public interface IRecommendationEngine
{
    Task<List<ProductRecommendation>> GetPersonalizedRecommendationsAsync(string userId, int count = 10);
    Task<List<ProductRecommendation>> GetSimilarProductsAsync(int productId, int count = 6);
    Task<List<ProductRecommendation>> GetFrequentlyBoughtTogetherAsync(int productId, int count = 4);
    Task<List<ProductRecommendation>> GetTrendingProductsAsync(int? categoryId = null, int count = 12);
    Task<List<ProductRecommendation>> GetCartRecommendationsAsync(List<int> cartProductIds, int count = 4);
    Task<List<ProductRecommendation>> GetRecentlyViewedBasedAsync(string userId, int count = 6);
    Task<List<ProductRecommendation>> GetCategoryRecommendationsAsync(int categoryId, string? userId, int count = 12);
    Task UpdateUserProfileAsync(string userId);
    Task TrainRecommendationModelAsync();
}
