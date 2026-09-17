using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Intelligence;
using Sparkle.Domain.System; // Correct namespace for SearchAnalytics
using System.Text.RegularExpressions;

namespace Sparkle.Infrastructure.Intelligence;

/// <summary>
/// NLP-powered smart search service
/// Provides query understanding, spell correction, and personalized ranking
/// </summary>
public class SmartSearchService : ISmartSearchService
{
    private readonly ApplicationDbContext _db;
    
    // Common synonyms for e-commerce
    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        { "phone", new[] { "mobile", "smartphone", "cell", "handset" } },
        { "laptop", new[] { "notebook", "computer", "pc" } },
        { "shirt", new[] { "top", "tee", "t-shirt", "blouse" } },
        { "pants", new[] { "trousers", "jeans", "bottoms", "slacks" } },
        { "shoes", new[] { "footwear", "sneakers", "boots", "sandals" } },
        { "bag", new[] { "purse", "handbag", "tote", "backpack" } },
        { "watch", new[] { "timepiece", "smartwatch", "wristwatch" } },
        { "headphones", new[] { "earbuds", "earphones", "headset", "airdots" } },
        { "camera", new[] { "dslr", "mirrorless", "cam" } },
        { "tv", new[] { "television", "smart tv", "led tv" } },
        { "cheap", new[] { "affordable", "budget", "inexpensive", "low price" } },
        { "best", new[] { "top", "popular", "recommended", "highest rated" } }
    };

    // Price keywords
    private static readonly Dictionary<string, (decimal? min, decimal? max)> PriceKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        { "cheap", (null, 500) },
        { "budget", (null, 1000) },
        { "affordable", (null, 2000) },
        { "mid-range", (2000, 10000) },
        { "premium", (10000, 50000) },
        { "luxury", (50000, null) },
        { "expensive", (20000, null) }
    };

    // Intent patterns
    private static readonly Dictionary<string, Regex> IntentPatterns = new()
    {
        { "Compare", new Regex(@"\b(vs|versus|compare|comparison|better|best)\b", RegexOptions.IgnoreCase) },
        { "Buy", new Regex(@"\b(buy|purchase|order|get|want)\b", RegexOptions.IgnoreCase) },
        { "Research", new Regex(@"\b(review|rating|specs|specification|features)\b", RegexOptions.IgnoreCase) }
    };

    public SmartSearchService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SearchQueryAnalysis> AnalyzeQueryAsync(string query, string? userId = null)
    {
        var normalized = NormalizeQuery(query);
        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        
        var analysis = new SearchQueryAnalysis
        {
            OriginalQuery = query,
            NormalizedQuery = normalized,
            ExtractedKeywords = words
        };

        // Extract synonyms
        var allSynonyms = new List<string>();
        foreach (var word in words)
        {
            if (Synonyms.TryGetValue(word, out var syns))
            {
                allSynonyms.AddRange(syns);
            }
        }
        analysis.Synonyms = allSynonyms.Distinct().ToList();

        // Detect category from query
        var detectedCategory = await DetectCategoryAsync(words);
        analysis.DetectedCategory = detectedCategory;

        // Detect brand
        var detectedBrand = await DetectBrandAsync(words);
        analysis.DetectedBrand = detectedBrand;

        // Detect price range
        foreach (var word in words)
        {
            if (PriceKeywords.TryGetValue(word, out var priceRange))
            {
                analysis.DetectedPriceMax = priceRange.max;
                analysis.InferredFilters["MaxPrice"] = priceRange.max ?? 0;
                if (priceRange.min.HasValue)
                    analysis.InferredFilters["MinPrice"] = priceRange.min.Value;
                break;
            }
        }

        // Detect search intent
        foreach (var (intent, pattern) in IntentPatterns)
        {
            if (pattern.IsMatch(query))
            {
                analysis.SearchIntent = intent;
                analysis.IntentConfidence = 0.8;
                break;
            }
        }

        // Generate search suggestions
        analysis.SuggestedQueries = await GenerateSuggestionsAsync(normalized);

        // Spelling corrections
        analysis.SpellingSuggestions = await GetSpellCorrectionsAsync(query);

        // Build inferred filters
        if (!string.IsNullOrEmpty(detectedCategory))
            analysis.InferredFilters["Category"] = detectedCategory;
        if (!string.IsNullOrEmpty(detectedBrand))
            analysis.InferredFilters["Brand"] = detectedBrand;

        return analysis;
    }

    public async Task<List<string>> GetSearchSuggestionsAsync(string partialQuery, int count = 8)
    {
        var suggestions = new List<string>();
        var normalized = NormalizeQuery(partialQuery);

        // Search in popular search queries
        var popularSearches = await _db.SearchAnalytics
            .Where(s => s.SearchQuery.Contains(normalized))
            .GroupBy(s => s.SearchQuery.ToLower())
            .OrderByDescending(g => g.Count())
            .Take(count)
            .Select(g => g.Key)
            .ToListAsync();
        
        suggestions.AddRange(popularSearches);

        // Search in product titles
        var productSuggestions = await _db.Products
            .Where(p => p.IsActive && p.Title.Contains(normalized))
            .Take(count)
            .Select(p => p.Title)
            .ToListAsync();
        
        suggestions.AddRange(productSuggestions);

        // Search in categories
        var categorySuggestions = await _db.Categories
            .Where(c => c.Name.Contains(normalized))
            .Take(3)
            .Select(c => c.Name)
            .ToListAsync();
        
        suggestions.AddRange(categorySuggestions);

        return suggestions.Distinct().Take(count).ToList();
    }

    public async Task<List<string>> GetSpellCorrectionsAsync(string query)
    {
        var corrections = new List<string>();
        var words = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        // Check against known product terms and categories
        var allTerms = await _db.Products
            .Select(p => p.Title)
            .Take(1000)
            .ToListAsync();

        var termWords = allTerms
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Distinct()
            .ToList();

        foreach (var word in words)
        {
            if (word.Length < 3) continue;
            
            // Simple Levenshtein-like check
            var closest = termWords
                .Where(t => t.Length >= word.Length - 1 && t.Length <= word.Length + 1)
                .Where(t => !t.Equals(word, StringComparison.OrdinalIgnoreCase))
                .Where(t => CalculateSimilarity(word, t) > 0.7)
                .Take(3)
                .ToList();

            if (closest.Any())
            {
                corrections.Add($"{word} → {closest.First()}");
            }
        }

        return corrections;
    }

    public async Task<List<int>> RankSearchResultsAsync(string query, List<int> productIds, string? userId = null)
    {
        if (!productIds.Any()) return productIds;

        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Title, p.AverageRating, p.PurchaseCount, p.TotalReviews })
            .ToListAsync();

        var queryLower = query.ToLowerInvariant();
        
        // Calculate relevance scores
        var scored = products.Select(p =>
        {
            double score = 0;
            
            // Title match
            if (p.Title.ToLowerInvariant().Contains(queryLower))
                score += 1.0;
            
            // Rating boost
            score += (double)p.AverageRating / 5.0 * 0.3;
            
            // Sales boost
            score += Math.Min(0.3, p.PurchaseCount / 1000.0);
            
            // Reviews boost
            score += Math.Min(0.2, p.TotalReviews / 100.0);

            return new { p.Id, Score = score };
        })
        .OrderByDescending(x => x.Score)
        .Select(x => x.Id)
        .ToList();

        return scored;
    }

    public async Task RecordSearchAsync(string userId, string query, int resultCount, int? clickedProductId = null)
    {
        var searchRecord = new SearchAnalytics
        {
            UserId = userId,
            SearchQuery = query,
            ResultCount = resultCount,
            ClickedProductId = clickedProductId,
            SearchedAt = DateTime.UtcNow
        };

        await _db.SearchAnalytics.AddAsync(searchRecord);
        await _db.SaveChangesAsync();
    }

    // Helper methods
    private static string NormalizeQuery(string query)
    {
        // Convert to lowercase, remove extra spaces, special characters
        var normalized = Regex.Replace(query.ToLowerInvariant(), @"[^\w\s-]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();
        return normalized;
    }

    private async Task<string?> DetectCategoryAsync(List<string> words)
    {
        var categories = await _db.Categories
            .Select(c => c.Name.ToLower())
            .ToListAsync();

        foreach (var word in words)
        {
            if (categories.Contains(word))
                return word;
            
            // Partial match
            var match = categories.FirstOrDefault(c => c.Contains(word));
            if (match != null)
                return match;
        }

        return null;
    }

    private async Task<string?> DetectBrandAsync(List<string> words)
    {
        var brands = await _db.Brands
            .Select(b => b.Name.ToLower())
            .ToListAsync();

        foreach (var word in words)
        {
            if (brands.Contains(word))
                return word;
        }

        return null;
    }

    private async Task<List<string>> GenerateSuggestionsAsync(string query)
    {
        // Generate related search suggestions
        var suggestions = await _db.SearchAnalytics
            .Where(s => s.SearchQuery.StartsWith(query) && s.SearchQuery != query)
            .GroupBy(s => s.SearchQuery)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToListAsync();

        return suggestions;
    }

    private static double CalculateSimilarity(string s1, string s2)
    {
        // Simple similarity check
        s1 = s1.ToLowerInvariant();
        s2 = s2.ToLowerInvariant();
        
        var longer = s1.Length > s2.Length ? s1 : s2;
        var shorter = s1.Length > s2.Length ? s2 : s1;
        
        if (longer.Length == 0) return 1.0;
        
        var matches = shorter.Count(c => longer.Contains(c));
        return (double)matches / longer.Length;
    }
}
