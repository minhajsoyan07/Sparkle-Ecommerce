using Sparkle.Domain.Intelligence;
using Sparkle.Domain.Catalog;

namespace Sparkle.Infrastructure.Intelligence;


/// <summary>
/// NLP-powered sentiment analysis service
/// </summary>
public interface ISentimentAnalyzer
{
    Task<SentimentAnalysis> AnalyzeReviewAsync(int reviewId, string content);
    Task<SentimentAnalysis> AnalyzeMessageAsync(string content, string contextType);
    Task<List<SentimentAnalysis>> BatchAnalyzeAsync(List<(int id, string content)> items);
    
    // Insights
    Task<Dictionary<string, double>> GetProductSentimentTrendAsync(int productId, int days = 30);
    Task<Dictionary<string, double>> GetSellerSentimentScoreAsync(int sellerId);
}

/// <summary>
/// ML-powered fraud detection service
/// </summary>
public interface IFraudDetector
{
    Task<FraudAnalysis> AnalyzeOrderAsync(int orderId);
    Task<FraudAnalysis> AnalyzeUserSessionAsync(string userId, string ipAddress, string userAgent);
    Task<bool> ShouldBlockTransactionAsync(int orderId);
    Task ReportFraudAsync(int orderId, string reason);
    Task UpdateFraudModelAsync();
}


/// <summary>
/// ML-powered dynamic pricing service
/// </summary>
public interface IDynamicPricingService
{
    Task<PricingSuggestion> GetPricingSuggestionAsync(int productId);
    Task<List<PricingSuggestion>> GetCategoryPricingSuggestionsAsync(int categoryId);
    Task<decimal> GetOptimalDiscountAsync(int productId, decimal targetSalesIncrease);
    Task UpdatePricingModelAsync();
}

/// <summary>
/// ML-powered customer intelligence service
/// </summary>
public interface ICustomerIntelligence
{
    Task<UserBehaviorProfile> GetUserProfileAsync(string userId);
    Task<List<CustomerSegment>> GetAllSegmentsAsync();
    Task<CustomerSegment?> GetUserSegmentAsync(string userId);
    Task<double> CalculateChurnRiskAsync(string userId);
    Task<double> CalculateLifetimeValueAsync(string userId);
    Task RefreshSegmentationAsync();
}
