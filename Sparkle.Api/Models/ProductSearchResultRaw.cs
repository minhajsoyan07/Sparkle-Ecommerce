namespace Sparkle.Api.Models;

// Raw result from stored procedure (SmartTags as comma-separated string)
internal class ProductSearchResultRaw
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
    public int ConfidenceScore { get; set; }
    public string? SmartTags { get; set; } // Comma-separated from SQL
    // public string? AppliedFilters { get; set; } // Removed to fix SQL column mismatch
    public string? PersonalizationLevel { get; set; }
    public bool IsFuzzyMatch { get; set; }
}
