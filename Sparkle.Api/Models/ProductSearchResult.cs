namespace Sparkle.Api.Models;

public class ProductSearchResult
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string? ShortDescription { get; set; }
    public decimal BasePrice { get; set; }
    public decimal? DiscountPercent { get; set; }
    public string? Thumbnail { get; set; }
    public int StockQuantity { get; set; }
    public string Slug { get; set; } = default!;
    public string CategoryName { get; set; } = default!;
    public string SellerName { get; set; } = default!;
    public int Relevance { get; set; }
    
    // AI-Powered Search Metadata
    public int ConfidenceScore { get; set; } // 0-100 match quality
    public List<string> SmartTags { get; set; } = new(); // "Best match", "Trending", "Recommended"
    public Dictionary<string, string> AppliedFilters { get; set; } = new(); // Auto-detected filters
    public string? PersonalizationLevel { get; set; } // "High", "Medium", "Low", null
    public bool IsFuzzyMatch { get; set; } // Typo correction was used
    
    // Display Helpers
    public decimal Price => BasePrice;
    public decimal FinalPrice => DiscountPercent.HasValue 
        ? BasePrice * (1 - (DiscountPercent.Value / 100m)) 
        : BasePrice;
}
