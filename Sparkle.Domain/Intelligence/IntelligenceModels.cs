namespace Sparkle.Domain.Intelligence;

/// <summary>
/// Product recommendation with ML scoring
/// </summary>
public class ProductRecommendation
{
    public int ProductId { get; set; }
    public string RecommendationType { get; set; } = string.Empty; // Collaborative, ContentBased, Trending, PersonalizedML
    public double ConfidenceScore { get; set; } // 0.0 to 1.0
    public double RelevanceScore { get; set; }
    public string? Reason { get; set; } // Internal reason for recommendation
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// User behavior profile for ML personalization
/// </summary>
public class UserBehaviorProfile
{
    public string UserId { get; set; } = string.Empty;
    
    // Category Preferences (ML-derived)
    public Dictionary<int, double> CategoryAffinityScores { get; set; } = new();
    public Dictionary<int, double> BrandAffinityScores { get; set; } = new();
    
    // Price Sensitivity
    public decimal PreferredPriceRangeLow { get; set; }
    public decimal PreferredPriceRangeHigh { get; set; }
    public double PriceSensitivityScore { get; set; } // 0=price insensitive, 1=very sensitive
    
    // Shopping Patterns
    public double ImpulseBuyerScore { get; set; }
    public double ResearcherScore { get; set; } // Compares many products before buying
    public double LoyaltyScore { get; set; }
    public double ChurnRiskScore { get; set; }
    
    // Engagement Metrics
    public TimeSpan AvgSessionDuration { get; set; }
    public double CartAbandonmentRate { get; set; }
    public double ConversionRate { get; set; }
    
    // Preferred Features
    public List<string> InferredInterests { get; set; } = new();
    public string PredictedNextCategory { get; set; } = string.Empty;
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Sentiment analysis result for reviews/messages
/// </summary>
public class SentimentAnalysis
{
    public int EntityId { get; set; }
    public string EntityType { get; set; } = string.Empty; // Review, ChatMessage, SupportTicket
    
    public string Sentiment { get; set; } = "Neutral"; // Positive, Negative, Neutral, Mixed
    public double SentimentScore { get; set; } // -1.0 (negative) to 1.0 (positive)
    public double ConfidenceScore { get; set; }
    
    // Aspect-based sentiment
    public Dictionary<string, double> AspectScores { get; set; } = new(); // Quality: 0.8, Delivery: -0.3
    
    // Key phrases extracted
    public List<string> KeyPhrases { get; set; } = new();
    public List<string> DetectedIssues { get; set; } = new();
    
    // Urgency/Priority
    public double UrgencyScore { get; set; }
    public bool RequiresAttention { get; set; }
    
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Fraud detection result
/// </summary>
public class FraudAnalysis
{
    public int OrderId { get; set; }
    public string UserId { get; set; } = string.Empty;
    
    public double FraudScore { get; set; } // 0.0 to 1.0
    public string RiskLevel { get; set; } = "Low"; // Low, Medium, High, Critical
    public bool IsBlocked { get; set; }
    public bool RequiresManualReview { get; set; }
    
    // Risk Factors
    public List<string> RiskFactors { get; set; } = new();
    public Dictionary<string, double> RiskBreakdown { get; set; } = new(); // VelocityRisk: 0.3, AddressRisk: 0.2
    
    // Device/Session Info
    public string? DeviceFingerprint { get; set; }
    public bool IsNewDevice { get; set; }
    public bool IsProxyDetected { get; set; }
    public bool IsLocationMismatch { get; set; }
    
    public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Smart search query analysis
/// </summary>
public class SearchQueryAnalysis
{
    public string OriginalQuery { get; set; } = string.Empty;
    public string NormalizedQuery { get; set; } = string.Empty;
    
    // NLP Extraction
    public List<string> ExtractedKeywords { get; set; } = new();
    public List<string> Synonyms { get; set; } = new();
    public string? DetectedCategory { get; set; }
    public string? DetectedBrand { get; set; }
    public decimal? DetectedPriceMax { get; set; }
    
    // Intent Classification
    public string SearchIntent { get; set; } = "Browse"; // Browse, Compare, Buy, Research
    public double IntentConfidence { get; set; }
    
    // Query Expansion
    public List<string> SuggestedQueries { get; set; } = new();
    public List<string> SpellingSuggestions { get; set; } = new();
    
    // Filters to auto-apply
    public Dictionary<string, object> InferredFilters { get; set; } = new();
}

/// <summary>
/// Dynamic pricing suggestion
/// </summary>
public class PricingSuggestion
{
    public int ProductId { get; set; }
    public decimal CurrentPrice { get; set; }
    public decimal SuggestedPrice { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    
    public string Strategy { get; set; } = string.Empty; // Competitive, Demand, MarginOptimized
    public double ConfidenceScore { get; set; }
    
    public List<string> Factors { get; set; } = new(); // Competitor pricing, Demand surge, etc.
    public decimal? CompetitorAvgPrice { get; set; }
    public double DemandScore { get; set; }
    public double ElasticityScore { get; set; }
    
    public DateTime ValidUntil { get; set; }
}

/// <summary>
/// Customer segment for targeted marketing
/// </summary>
public class CustomerSegment
{
    public string SegmentId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    public List<string> UserIds { get; set; } = new();
    public int UserCount { get; set; }
    
    // Segment Characteristics
    public Dictionary<string, double> Characteristics { get; set; } = new();
    public decimal AvgOrderValue { get; set; }
    public double AvgPurchaseFrequency { get; set; }
    public double LifetimeValueScore { get; set; }
    
    // Recommended Actions
    public List<string> RecommendedCampaigns { get; set; } = new();
    public List<int> RecommendedProducts { get; set; } = new();
    
    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Product similarity for "Similar Products" feature
/// </summary>
public class ProductSimilarity
{
    public int ProductId { get; set; }
    public int SimilarProductId { get; set; }
    public double SimilarityScore { get; set; } // 0.0 to 1.0
    public string SimilarityType { get; set; } = string.Empty; // Visual, Attribute, Behavioral, Textual
}
