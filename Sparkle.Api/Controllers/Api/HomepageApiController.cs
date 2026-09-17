using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;
using Sparkle.Api.Services;
using System.Text.Json;

namespace Sparkle.Api.Controllers.Api;

/// <summary>
/// Public API controller for homepage sections and intelligent product recommendations
/// Used by frontend to fetch sections and products for display
/// No admin functionality - read-only access
/// </summary>
[ApiController]
[Route("api/homepage")]
public class HomepageApiController : ControllerBase
{
    private readonly IHomepageSectionService _sectionService;
    private readonly IIntelligentProductAnalysisService _analysisService;
    private readonly IDistributedCache _cache;
    private readonly ILogger<HomepageApiController> _logger;

    public HomepageApiController(
        IHomepageSectionService sectionService,
        IIntelligentProductAnalysisService analysisService,
        IDistributedCache cache,
        ILogger<HomepageApiController> logger)
    {
        _sectionService = sectionService;
        _analysisService = analysisService;
        _cache = cache;
        _logger = logger;
    }

    // ==================== SECTION RETRIEVAL ====================

    /// <summary>
    /// Gets all active homepage sections with their configuration
    /// Cached for performance (1 hour)
    /// </summary>
    [HttpGet("sections")]
    [ResponseCache(Duration = 3600, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResponse<List<HomepageSectionDisplayDto>>>> GetHomepageSections()
    {
        try
        {
            // Try to get from cache
            var cachedSections = await _cache.GetStringAsync("homepage:sections");
            if (!string.IsNullOrEmpty(cachedSections))
            {
                var cached = JsonSerializer.Deserialize<ApiResponse<List<HomepageSectionDisplayDto>>>(cachedSections);
                if (cached != null)
                    return Ok(cached);
            }

            var sections = await _sectionService.GetAllActiveSectionsAsync();
            var dtos = new List<HomepageSectionDisplayDto>();

            foreach (var section in sections)
            {
                var products = await _sectionService.GetSectionProductsAsync(section.Id, section.MaxProductsToDisplay);
                var categories = await _sectionService.GetSectionCategoriesAsync(section.Id);

                var dto = new HomepageSectionDisplayDto
                {
                    Id = section.Id,
                    Name = section.Name ?? string.Empty,
                    Slug = section.Slug ?? string.Empty,
                    DisplayTitle = section.DisplayTitle ?? string.Empty,
                    Description = section.Description,
                    SectionType = section.SectionType ?? string.Empty,
                    DisplayOrder = section.DisplayOrder,
                    LayoutType = section.LayoutType,
                    CardSize = section.CardSize,
                    ProductsPerRow = section.ProductsPerRow,
                    ShowRating = section.ShowRating,
                    ShowPrice = section.ShowPrice,
                    ShowDiscount = section.ShowDiscount,
                    BackgroundColor = section.BackgroundColor,
                    BannerImageUrl = section.BannerImageUrl,
                    Products = products.Select(p => new SimplifiedProductDto
                    {
                        Id = p.Id,
                        Title = p.Title,
                        RegularPrice = p.BasePrice,
                        DiscountedPrice = p.Price,
                        ThumbnailUrl = p.Images?.FirstOrDefault()?.Url,
                        Rating = p.AverageRating,
                        ReviewCount = p.TotalReviews,
                        StockStatus = (p.Variants?.Sum(v => v.Stock) ?? 0) > 0 ? "In Stock" : "Out of Stock"
                    }).ToList(),
                    Categories = (List<dynamic>)categories.Select(c => new { c.Id, c.Name }).Cast<dynamic>().ToList()
                };
                dtos.Add(dto);
            }

            var response = new ApiResponse<List<HomepageSectionDisplayDto>>
            {
                Success = true,
                Data = dtos.OrderBy(d => d.DisplayOrder).ToList(),
                Message = $"Retrieved {dtos.Count} homepage sections"
            };

            // Cache the response
            await _cache.SetStringAsync(
                "homepage:sections",
                JsonSerializer.Serialize(response),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1) });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving homepage sections");
            return BadRequest(new ApiResponse<object>
            {
                Success = false,
                Message = "Error retrieving sections"
            });
        }
    }

    /// <summary>
    /// Gets a specific section with products by slug
    /// </summary>
    [HttpGet("sections/{slug}")]
    [ResponseCache(Duration = 1800, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResponse<HomepageSectionDisplayDto>>> GetSectionBySlug(string slug)
    {
        try
        {
            var section = await _sectionService.GetSectionBySlugAsync(slug);
            if (section == null)
                return NotFound(new ApiResponse<object> { Success = false, Message = "Section not found" });

            var products = await _sectionService.GetSectionProductsAsync(section.Id, section.MaxProductsToDisplay);
            var categories = await _sectionService.GetSectionCategoriesAsync(section.Id);

            var dto = new HomepageSectionDisplayDto
            {
                Id = section.Id,
                Name = section.Name ?? string.Empty,
                Slug = section.Slug ?? string.Empty,
                DisplayTitle = section.DisplayTitle ?? string.Empty,
                Description = section.Description,
                SectionType = section.SectionType ?? string.Empty,
                DisplayOrder = section.DisplayOrder,
                LayoutType = section.LayoutType,
                CardSize = section.CardSize,
                ProductsPerRow = section.ProductsPerRow,
                ShowRating = section.ShowRating,
                ShowPrice = section.ShowPrice,
                ShowDiscount = section.ShowDiscount,
                BackgroundColor = section.BackgroundColor,
                BannerImageUrl = section.BannerImageUrl,
                Products = products.Select(p => new SimplifiedProductDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    RegularPrice = p.BasePrice,
                    DiscountedPrice = p.Price,
                    ThumbnailUrl = p.Images?.FirstOrDefault()?.Url,
                    Rating = p.AverageRating,
                    ReviewCount = p.TotalReviews,
                    StockStatus = (p.Variants?.Sum(v => v.Stock) ?? 0) > 0 ? "In Stock" : "Out of Stock"
                }).ToList(),
                Categories = (List<dynamic>)categories.Select(c => new { c.Id, c.Name }).Cast<dynamic>().ToList()
            };

            return Ok(new ApiResponse<HomepageSectionDisplayDto>
            {
                Success = true,
                Data = dto
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving section");
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Error retrieving section" });
        }
    }

    // ==================== TRENDING PRODUCTS ====================

    /// <summary>
    /// Gets current trending products
    /// Based on intelligent analysis of sales velocity, search volume, wishlist adds
    /// Cached (30 minutes)
    /// </summary>
    [HttpGet("trending")]
    [ResponseCache(Duration = 1800, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResponse<List<SimplifiedProductDto>>>> GetTrendingProducts([FromQuery] int limit = 12)
    {
        try
        {
            // Cache key
            var cacheKey = $"homepage:trending:{limit}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedData = JsonSerializer.Deserialize<ApiResponse<List<SimplifiedProductDto>>>(cached);
                if (cachedData != null)
                    return Ok(cachedData);
            }

            var products = await _analysisService.GetCurrentTrendingProductsAsync(limit);

            var dto = products.Select(p => new SimplifiedProductDto
            {
                Id = p.Id,
                Title = p.Title,
                RegularPrice = p.BasePrice,
                DiscountedPrice = p.Price,
                ThumbnailUrl = p.Images?.FirstOrDefault()?.Url,
                Rating = p.AverageRating,
                ReviewCount = p.TotalReviews,
                StockStatus = (p.Variants?.Sum(v => v.Stock) ?? 0) > 0 ? "In Stock" : "Out of Stock"
            }).ToList();

            var response = new ApiResponse<List<SimplifiedProductDto>>
            {
                Success = true,
                Data = dto,
                Message = $"Retrieved {dto.Count} trending products"
            };

            // Cache
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(response),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving trending products");
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Error retrieving trending products" });
        }
    }

    /// <summary>
    /// Gets suggested flash sale products
    /// Based on intelligent analysis of inventory levels and pricing strategy
    /// Cached (30 minutes)
    /// </summary>
    [HttpGet("flash-sale-suggestions")]
    [ResponseCache(Duration = 1800, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResponse<List<FlashSaleSuggestionDto>>>> GetFlashSaleSuggestions([FromQuery] int limit = 10)
    {
        try
        {
            var cacheKey = $"homepage:flash-sale:{limit}";
            var cached = await _cache.GetStringAsync(cacheKey);
            if (!string.IsNullOrEmpty(cached))
            {
                var cachedData = JsonSerializer.Deserialize<ApiResponse<List<FlashSaleSuggestionDto>>>(cached);
                if (cachedData != null)
                    return Ok(cachedData);
            }

            var products = await _analysisService.GetSuggestedFlashSaleProductsAsync(limit);

            var dto = products.Select(p => new FlashSaleSuggestionDto
            {
                Id = p.Id,
                Title = p.Title,
                RegularPrice = p.BasePrice,
                ThumbnailUrl = p.Images?.FirstOrDefault()?.Url,
                StockStatus = (p.Variants?.Sum(v => v.Stock) ?? 0) > 0 ? "In Stock" : "Out of Stock"
            }).ToList();

            var response = new ApiResponse<List<FlashSaleSuggestionDto>>
            {
                Success = true,
                Data = dto,
                Message = $"Retrieved {dto.Count} flash sale suggestions"
            };

            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(response),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving flash sale suggestions");
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Error retrieving flash sale suggestions" });
        }
    }

    // ==================== RECOMMENDATIONS ====================

    /// <summary>
    /// Gets personalized product recommendations for logged-in user
    /// Based on browsing history and similar user behavior
    /// </summary>
    [HttpGet("recommendations")]
    [ResponseCache(Duration = 600, VaryByHeader = "Cookie", Location = ResponseCacheLocation.Any)]
    public async Task<ActionResult<ApiResponse<List<SimplifiedProductDto>>>> GetRecommendations([FromQuery] int limit = 12)
    {
        try
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            
            if (string.IsNullOrEmpty(userId))
            {
                // Return trending for non-logged-in users
                var trendingProducts = await _analysisService.GetCurrentTrendingProductsAsync(limit);
                var trendingDto = trendingProducts.Select(p => new SimplifiedProductDto
                {
                    Id = p.Id,
                    Title = p.Title,
                    RegularPrice = p.BasePrice,
                    DiscountedPrice = p.Price,
                    ThumbnailUrl = p.Images?.FirstOrDefault()?.Url,
                    Rating = p.AverageRating,
                    ReviewCount = p.TotalReviews,
                    StockStatus = (p.Variants?.Sum(v => v.Stock) ?? 0) > 0 ? "In Stock" : "Out of Stock"
                }).ToList();

                return Ok(new ApiResponse<List<SimplifiedProductDto>>
                {
                    Success = true,
                    Data = trendingDto,
                    Message = "Personalized recommendations not available. Showing trending products."
                });
            }

            // Get personalized recommendations
            var productIds = await _analysisService.GetRecommendedProductsForUserAsync(userId, limit);
            
            // In production, fetch full product data
            var products = new List<SimplifiedProductDto>();
            // This would be enhanced to fetch actual product data

            return Ok(new ApiResponse<List<SimplifiedProductDto>>
            {
                Success = true,
                Data = products,
                Message = $"Retrieved {products.Count} personalized recommendations"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recommendations");
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Error retrieving recommendations" });
        }
    }
}

// ==================== DTOs ====================

public class HomepageSectionDisplayDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SectionType { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public string LayoutType { get; set; } = string.Empty;
    public string CardSize { get; set; } = string.Empty;
    public int ProductsPerRow { get; set; }
    public bool ShowRating { get; set; }
    public bool ShowPrice { get; set; }
    public bool ShowDiscount { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BannerImageUrl { get; set; }
    public List<SimplifiedProductDto> Products { get; set; } = new();
    public List<object> Categories { get; set; } = new();
}

public class SimplifiedProductDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal RegularPrice { get; set; }
    public decimal DiscountedPrice { get; set; }
    public string? ThumbnailUrl { get; set; }
    public decimal Rating { get; set; }
    public int ReviewCount { get; set; }
    public string StockStatus { get; set; } = string.Empty;
}

public class FlashSaleSuggestionDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal RegularPrice { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string StockStatus { get; set; } = string.Empty;
}
