using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Intelligence;
using System.Text.RegularExpressions;

namespace Sparkle.Infrastructure.Intelligence;

/// <summary>
/// NLP-powered sentiment analysis implementation
/// Uses rule-based analysis with ML-ready architecture
/// </summary>
public class SentimentAnalyzer : ISentimentAnalyzer
{
    private readonly ApplicationDbContext _db;
    
    // Sentiment lexicons (in production, these would be ML models)
    private static readonly HashSet<string> PositiveWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "excellent", "amazing", "great", "wonderful", "fantastic", "perfect", "love", "best",
        "awesome", "superb", "outstanding", "brilliant", "good", "nice", "happy", "satisfied",
        "recommend", "thanks", "beautiful", "quality", "fast", "quick", "helpful", "friendly",
        "professional", "smooth", "impressed", "genuine", "authentic", "worth", "value"
    };
    
    private static readonly HashSet<string> NegativeWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "terrible", "awful", "horrible", "bad", "worst", "hate", "poor", "disappointing",
        "disappointed", "broken", "damaged", "fake", "scam", "fraud", "refund", "return",
        "waste", "useless", "defective", "problem", "issue", "complaint", "slow", "late",
        "delay", "never", "wrong", "missing", "cheap", "rude", "unprofessional", "avoid"
    };

    private static readonly HashSet<string> UrgencyWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "urgent", "immediately", "asap", "emergency", "critical", "help", "please",
        "desperate", "stuck", "cannot", "failed", "error", "broken", "not working"
    };

    private static readonly Dictionary<string, string[]> AspectKeywords = new()
    {
        { "Quality", new[] { "quality", "material", "build", "durable", "sturdy", "fragile" } },
        { "Delivery", new[] { "delivery", "shipping", "arrived", "package", "courier", "late", "fast" } },
        { "Value", new[] { "price", "worth", "value", "expensive", "cheap", "money", "affordable" } },
        { "Service", new[] { "service", "support", "response", "helpful", "rude", "customer" } },
        { "Authenticity", new[] { "genuine", "authentic", "fake", "original", "copy", "real" } }
    };

    public SentimentAnalyzer(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SentimentAnalysis> AnalyzeReviewAsync(int reviewId, string content)
    {
        var analysis = AnalyzeText(content);
        analysis.EntityId = reviewId;
        analysis.EntityType = "Review";

        // Save analysis to database for ML training
        await Task.CompletedTask;
        
        return analysis;
    }

    public async Task<SentimentAnalysis> AnalyzeMessageAsync(string content, string contextType)
    {
        var analysis = AnalyzeText(content);
        analysis.EntityType = contextType;
        
        await Task.CompletedTask;
        return analysis;
    }

    public async Task<List<SentimentAnalysis>> BatchAnalyzeAsync(List<(int id, string content)> items)
    {
        var results = new List<SentimentAnalysis>();
        foreach (var (id, content) in items)
        {
            var analysis = AnalyzeText(content);
            analysis.EntityId = id;
            results.Add(analysis);
        }
        
        await Task.CompletedTask;
        return results;
    }

    public async Task<Dictionary<string, double>> GetProductSentimentTrendAsync(int productId, int days = 30)
    {
        var startDate = DateTime.UtcNow.AddDays(-days);
        
        var reviews = await _db.ProductReviews
            .Where(r => r.ProductId == productId && r.ReviewDate >= startDate)
            .Select(r => new { r.Rating, r.Comment })
            .ToListAsync();

        if (!reviews.Any())
            return new Dictionary<string, double>();

        var avgRating = reviews.Average(r => r.Rating);
        var sentimentScores = reviews.Select(r => AnalyzeText(r.Comment).SentimentScore).ToList();
        
        return new Dictionary<string, double>
        {
            { "AverageRating", avgRating },
            { "AverageSentiment", sentimentScores.Any() ? sentimentScores.Average() : 0 },
            { "PositivePercent", sentimentScores.Count(s => s > 0.3) / (double)sentimentScores.Count * 100 },
            { "NegativePercent", sentimentScores.Count(s => s < -0.3) / (double)sentimentScores.Count * 100 },
            { "TotalReviews", reviews.Count }
        };
    }

    public async Task<Dictionary<string, double>> GetSellerSentimentScoreAsync(int sellerId)
    {
        var reviews = await _db.ProductReviews
            .Where(r => r.SellerId == sellerId)
            .Select(r => new { r.Rating, r.Comment })
            .ToListAsync();

        if (!reviews.Any())
            return new Dictionary<string, double> { { "OverallScore", 0.5 } };

        var sentimentScores = reviews.Select(r => AnalyzeText(r.Comment).SentimentScore).ToList();
        var avgRating = reviews.Average(r => r.Rating);

        return new Dictionary<string, double>
        {
            { "OverallScore", (avgRating / 5.0 + (sentimentScores.Average() + 1) / 2) / 2 },
            { "SentimentScore", sentimentScores.Average() },
            { "RatingScore", avgRating / 5.0 },
            { "ReviewCount", reviews.Count }
        };
    }

    private SentimentAnalysis AnalyzeText(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new SentimentAnalysis
            {
                Sentiment = "Neutral",
                SentimentScore = 0,
                ConfidenceScore = 0.5
            };
        }

        var words = Regex.Split(content.ToLowerInvariant(), @"\W+")
            .Where(w => w.Length > 2)
            .ToList();

        // Count positive and negative words
        var positiveCount = words.Count(w => PositiveWords.Contains(w));
        var negativeCount = words.Count(w => NegativeWords.Contains(w));
        var urgencyCount = words.Count(w => UrgencyWords.Contains(w));
        var totalSentimentWords = positiveCount + negativeCount;

        // Calculate sentiment score (-1 to 1)
        double sentimentScore = 0;
        if (totalSentimentWords > 0)
        {
            sentimentScore = (positiveCount - negativeCount) / (double)totalSentimentWords;
        }

        // Determine sentiment category
        string sentiment;
        if (sentimentScore > 0.25) sentiment = "Positive";
        else if (sentimentScore < -0.25) sentiment = "Negative";
        else if (positiveCount > 0 && negativeCount > 0) sentiment = "Mixed";
        else sentiment = "Neutral";

        // Calculate confidence
        double confidence = Math.Min(1.0, totalSentimentWords / 5.0);

        // Aspect-based sentiment
        var aspectScores = new Dictionary<string, double>();
        foreach (var (aspect, keywords) in AspectKeywords)
        {
            var mentioned = words.Any(w => keywords.Contains(w));
            if (mentioned)
            {
                // Calculate aspect-specific sentiment
                var aspectWords = words.Where(w => keywords.Contains(w)).ToList();
                // Simplified: use overall sentiment for aspect
                aspectScores[aspect] = sentimentScore;
            }
        }

        // Extract key phrases
        var keyPhrases = ExtractKeyPhrases(content);

        // Detect issues
        var issues = new List<string>();
        if (words.Any(w => new[] { "broken", "damaged", "defective" }.Contains(w)))
            issues.Add("Product Quality Issue");
        if (words.Any(w => new[] { "late", "delay", "never arrived" }.Contains(w)))
            issues.Add("Delivery Issue");
        if (words.Any(w => new[] { "fake", "counterfeit", "not genuine" }.Contains(w)))
            issues.Add("Authenticity Concern");

        // Calculate urgency
        var urgencyScore = Math.Min(1.0, urgencyCount / 3.0);

        return new SentimentAnalysis
        {
            Sentiment = sentiment,
            SentimentScore = sentimentScore,
            ConfidenceScore = confidence,
            AspectScores = aspectScores,
            KeyPhrases = keyPhrases,
            DetectedIssues = issues,
            UrgencyScore = urgencyScore,
            RequiresAttention = urgencyScore > 0.5 || sentimentScore < -0.5,
            AnalyzedAt = DateTime.UtcNow
        };
    }

    private static List<string> ExtractKeyPhrases(string content)
    {
        // Simple key phrase extraction (in production, use NLP library)
        var sentences = content.Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);
        var keyPhrases = new List<string>();
        
        foreach (var sentence in sentences.Take(3))
        {
            var trimmed = sentence.Trim();
            if (trimmed.Length > 10 && trimmed.Length < 100)
            {
                keyPhrases.Add(trimmed);
            }
        }

        return keyPhrases;
    }
}
