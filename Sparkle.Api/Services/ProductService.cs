using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Models;
using Sparkle.Infrastructure;
using Sparkle.Domain.Intelligence;
using System.Text.Json;

namespace Sparkle.Api.Services;

public interface IProductService
{
    Task<List<ProductSearchResult>> SearchProductsAsync(
        int? categoryId, 
        string? searchTerm, 
        decimal? minPrice, 
        decimal? maxPrice,
        Dictionary<string, string>? attributeFilters,
        string sortBy = "Relevance", 
        int page = 1, 
        int pageSize = 20);

    Task<List<Sparkle.Domain.Catalog.Product>> GetProductsByIdsAsync(List<int> ids);
}

public class ProductService : IProductService
{
    private readonly ApplicationDbContext _db;
    private readonly ISmartSearchService _smartSearch;

    public ProductService(ApplicationDbContext db, ISmartSearchService smartSearch)
    {
        _db = db;
        _smartSearch = smartSearch;
    }

    public async Task<List<ProductSearchResult>> SearchProductsAsync(
        int? categoryId, 
        string? searchTerm, 
        decimal? minPrice, 
        decimal? maxPrice,
        Dictionary<string, string>? attributeFilters,
        string sortBy = "Relevance", 
        int page = 1, 
        int pageSize = 20)
    {
        // 1. AI Query Analysis
        string? effectiveSearchTerm = searchTerm;
        
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            try 
            {
                var analysis = await _smartSearch.AnalyzeQueryAsync(searchTerm);
                
                // Use normalized query for better matching
                effectiveSearchTerm = analysis.NormalizedQuery;

                // Apply AI-inferred filters if not explicitly provided
                if (!maxPrice.HasValue && analysis.DetectedPriceMax.HasValue)
                {
                    maxPrice = analysis.DetectedPriceMax;
                }
                
                // If specific intent like "cheap", force price sort if not specified
                if (sortBy == "Relevance" && (analysis.DetectedPriceMax < 1000 || searchTerm.Contains("cheap")))
                {
                    sortBy = "PriceLowHigh";
                }
            }
            catch 
            {
                // Fallback to raw query if AI service fails
                effectiveSearchTerm = searchTerm;
            }
        }

        var attributeFiltersJson = attributeFilters != null && attributeFilters.Any() 
            ? JsonSerializer.Serialize(attributeFilters) 
            : null;

        var safeSortBy = sortBy switch 
        {
            "PriceLowHigh" => "PriceLowHigh",
            "PriceHighLow" => "PriceHighLow",
            _ => "Relevance"
        };
        
        // Ensure Page is at least 1
        if (page < 1) page = 1;

        // Execute stored procedure with AI metadata
        var rawResults = await _db.Database.SqlQuery<ProductSearchResultRaw>($@"
            EXEC [catalog].[usp_SearchProducts] 
            @CategoryId={categoryId}, 
            @SearchTerm={effectiveSearchTerm}, 
            @MinPrice={minPrice}, 
            @MaxPrice={maxPrice}, 
            @AttributeFilters={attributeFiltersJson}, 
            @SortBy={safeSortBy}, 
            @PageNumber={page}, 
            @PageSize={pageSize}
        ").ToListAsync();

        // Process SmartTags from comma-separated string to List
        var results = rawResults.Select(raw => new ProductSearchResult
        {
            Id = raw.Id,
            Title = raw.Title,
            ShortDescription = raw.ShortDescription,
            BasePrice = raw.BasePrice,
            DiscountPercent = raw.DiscountPercent,
            Thumbnail = raw.Thumbnail,
            StockQuantity = raw.StockQuantity,
            Slug = raw.Slug,
            CategoryName = raw.CategoryName,
            SellerName = raw.SellerName,
            Relevance = raw.Relevance,
            ConfidenceScore = raw.ConfidenceScore,
            SmartTags = string.IsNullOrWhiteSpace(raw.SmartTags) 
                ? new List<string>() 
                : raw.SmartTags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            AppliedFilters = new Dictionary<string, string>(),
            PersonalizationLevel = raw.PersonalizationLevel,
            IsFuzzyMatch = raw.IsFuzzyMatch
        }).ToList();

        return results;
    }

    public async Task<List<Sparkle.Domain.Catalog.Product>> GetProductsByIdsAsync(List<int> ids)
    {
        if (ids == null || !ids.Any()) return new List<Sparkle.Domain.Catalog.Product>();
        
        return await _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Variants.OrderBy(v => v.Id))
            .Include(p => p.Seller)
            .Where(p => ids.Contains(p.Id) && p.IsActive && p.Seller != null && p.Seller.Status == Sparkle.Domain.Sellers.SellerStatus.Approved)
            .ToListAsync();
    }
}
