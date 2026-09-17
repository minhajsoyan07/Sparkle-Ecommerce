using Sparkle.Domain.Common;
using Sparkle.Domain.Catalog;

namespace Sparkle.Infrastructure.Services;

public interface IAIService
{
    // Natural Language Processing
    Task<List<string>> ExtractKeywordsAsync(string query);
    Task<double> AnalyzeSentimentAsync(string text); // Returns -1.0 to 1.0

    // Machine Learning / Recommender
    double CalculateSimilarity(Product p1, Product p2);
    Task<List<Product>> GetRelatedProductsAsync(Product product, List<Product> candidates, int count = 5);
    
    // Predictive Analytics
    Task<int> PredictNextMonthSalesAsync(int productId, List<int> salesHistory);
}
