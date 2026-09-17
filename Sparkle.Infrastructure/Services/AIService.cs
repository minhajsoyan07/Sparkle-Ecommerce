using Sparkle.Domain.Catalog;
using System.Text.RegularExpressions;

namespace Sparkle.Infrastructure.Services;

public class AIService : IAIService
{
    // Simulating a stop-word list
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "an", "the", "in", "on", "at", "for", "to", "of", "with", "and", "or", "is", "are", "was", "were"
    };

    // NLP: Keyword Extraction
    public Task<List<string>> ExtractKeywordsAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return Task.FromResult(new List<string>());

        // Normalize: Lowercase -> Remove special chars -> Split
        var words = Regex.Replace(query.ToLower(), @"[^a-z0-9\s]", "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !StopWords.Contains(w)) // Filter noise
            .Distinct()
            .ToList();

        return Task.FromResult(words);
    }

    // NLP: Basic Sentiment Analysis (Rule-based)
    public Task<double> AnalyzeSentimentAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Task.FromResult(0.0);

        var positiveWords = new[] { "good", "great", "excellent", "amazing", "love", "best", "perfect", "fast", "reliable" };
        var negativeWords = new[] { "bad", "poor", "terrible", "awful", "hate", "worst", "slow", "broken", "waste" };

        var tokens = text.ToLower().Split(new[] { ' ', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
        
        int score = 0;
        foreach (var token in tokens)
        {
            if (positiveWords.Contains(token)) score++;
            if (negativeWords.Contains(token)) score--;
        }

        // Normalize to -1.0 to 1.0 range
        double normalizedScore = Math.Clamp(score * 0.2, -1.0, 1.0);
        return Task.FromResult(normalizedScore);
    }

    // ML: Content-Based Similarity (Weighted Vector)
    public double CalculateSimilarity(Product p1, Product p2)
    {
        double score = 0;

        // 1. Category Match (High Weight)
        if (p1.CategoryId == p2.CategoryId) score += 5.0;
        
        // 2. Brand Match (Medium Weight)
        if (p1.BrandId.HasValue && p2.BrandId.HasValue && p1.BrandId == p2.BrandId) score += 3.0;

        // 3. Price Vicinity (Low Weight) - Products in simplified price range are "similar"
        if (p1.Price > 0 && p2.Price > 0)
        {
            var priceRatio = Math.Min(p1.Price, p2.Price) / Math.Max(p1.Price, p2.Price);
            if (priceRatio > 0.7m) score += 2.0;
        }

        // 4. Tag Overlap (Medium Weight) - Assuming tags are comma-separated in meta-keywords or similar
        // (Simplified for now as Tags property might be missing in Product domain, using Title tokens instead)
        var p1Tokens = p1.Title.ToLower().Split(' ');
        var p2Tokens = p2.Title.ToLower().Split(' ');
        var commonTokens = p1Tokens.Intersect(p2Tokens).Count();
        score += commonTokens * 0.5;

        return score;
    }

    public Task<List<Product>> GetRelatedProductsAsync(Product product, List<Product> candidates, int count = 5)
    {
        var related = candidates
            .Where(p => p.Id != product.Id)
            .Select(p => new { Product = p, Score = CalculateSimilarity(product, p) })
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select(x => x.Product)
            .ToList();

        return Task.FromResult(related);
    }

    // ML: Simple Linear Regression for Sales Prediction
    public Task<int> PredictNextMonthSalesAsync(int productId, List<int> salesHistory)
    {
        if (salesHistory == null || salesHistory.Count < 2) return Task.FromResult(0);

        // Simple Least Squares Regression: y = mx + b
        // x = time index, y = sales count
        int n = salesHistory.Count;
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;

        for (int i = 0; i < n; i++)
        {
            sumX += i;
            sumY += salesHistory[i];
            sumXY += i * salesHistory[i];
            sumX2 += i * i;
        }

        double slope = (n * sumXY - sumX * sumY) / (n * sumX2 - sumX * sumX);
        double intercept = (sumY - slope * sumX) / n;

        // Predict for n (next month)
        double prediction = slope * n + intercept;
        
        return Task.FromResult((int)Math.Max(0, Math.Round(prediction)));
    }
}
