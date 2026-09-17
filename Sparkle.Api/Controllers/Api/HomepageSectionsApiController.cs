using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sparkle.Api.Services;
using Sparkle.Domain.Content;
using System.Security.Claims;

namespace Sparkle.Api.Controllers.Api;

/// <summary>
/// API controller for homepage section management
/// Provides endpoints for admin to manage product sections, layouts, and configurations
/// </summary>
[ApiController]
[Route("api/admin/homepage-sections")]
[Authorize(Roles = "Admin")]
public class HomepageSectionsApiController : ControllerBase
{
    private readonly IHomepageSectionService _sectionService;
    private readonly ILogger<HomepageSectionsApiController> _logger;

    public HomepageSectionsApiController(IHomepageSectionService sectionService, ILogger<HomepageSectionsApiController> logger)
    {
        _sectionService = sectionService;
        _logger = logger;
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    // ==================== SECTION MANAGEMENT ====================

    /// <summary>
    /// Gets all active homepage sections
    /// </summary>
    [HttpGet]
    [AllowAnonymous] // Read access is public
    public async Task<ActionResult<ApiResponse<List<HomepageSectionDto>>>> GetAllSections()
    {
        var sections = await _sectionService.GetAllActiveSectionsAsync();
        var dtos = sections.Select(MapToDto).ToList();
        
        return Ok(new ApiResponse<List<HomepageSectionDto>>
        {
            Success = true,
            Data = dtos,
            Message = $"Retrieved {dtos.Count} sections"
        });
    }

    /// <summary>
    /// Gets a specific section by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<HomepageSectionDto>>> GetSection(int id)
    {
        var section = await _sectionService.GetSectionByIdAsync(id);
        if (section == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Section not found" });

        return Ok(new ApiResponse<HomepageSectionDto>
        {
            Success = true,
            Data = MapToDto(section)
        });
    }

    /// <summary>
    /// Gets a section by slug
    /// </summary>
    [HttpGet("by-slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<HomepageSectionDto>>> GetSectionBySlug(string slug)
    {
        var section = await _sectionService.GetSectionBySlugAsync(slug);
        if (section == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Section not found" });

        return Ok(new ApiResponse<HomepageSectionDto>
        {
            Success = true,
            Data = MapToDto(section)
        });
    }

    /// <summary>
    /// Creates a new homepage section (Admin only)
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<HomepageSectionDto>>> CreateSection([FromBody] CreateHomepageSectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid input" });

        var section = new HomepageSection
        {
            Name = request.Name,
            Slug = request.Slug?.ToLower() ?? request.Name.ToLower().Replace(" ", "-"),
            DisplayTitle = request.DisplayTitle,
            Description = request.Description,
            SectionType = request.SectionType,
            DisplayOrder = request.DisplayOrder,
            IsActive = request.IsActive ?? false,
            MaxProductsToDisplay = request.MaxProductsToDisplay ?? 12,
            ProductsPerRow = request.ProductsPerRow ?? 4,
            LayoutType = request.LayoutType ?? "Grid",
            CardSize = request.CardSize ?? "Medium",
            UseAutomatedSelection = request.UseAutomatedSelection ?? true,
            BackgroundColor = request.BackgroundColor,
            BannerImageUrl = request.BannerImageUrl,
            ShowRating = request.ShowRating ?? true,
            ShowPrice = request.ShowPrice ?? true,
            ShowDiscount = request.ShowDiscount ?? true
        };

        var created = await _sectionService.CreateSectionAsync(section, GetUserId());
        
        return CreatedAtAction(nameof(GetSection), new { id = created.Id }, new ApiResponse<HomepageSectionDto>
        {
            Success = true,
            Data = MapToDto(created),
            Message = "Section created successfully"
        });
    }

    /// <summary>
    /// Updates a homepage section (Admin only)
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<HomepageSectionDto>>> UpdateSection(int id, [FromBody] UpdateHomepageSectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid input" });

        var section = await _sectionService.GetSectionByIdAsync(id);
        if (section == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Section not found" });

        // Update properties
        section.Name = request.Name ?? section.Name;
        section.DisplayTitle = request.DisplayTitle ?? section.DisplayTitle;
        section.Description = request.Description ?? section.Description;
        section.DisplayOrder = request.DisplayOrder ?? section.DisplayOrder;
        section.IsActive = request.IsActive ?? section.IsActive;
        section.MaxProductsToDisplay = request.MaxProductsToDisplay ?? section.MaxProductsToDisplay;
        section.ProductsPerRow = request.ProductsPerRow ?? section.ProductsPerRow;
        section.LayoutType = request.LayoutType ?? section.LayoutType;
        section.CardSize = request.CardSize ?? section.CardSize;
        section.BackgroundColor = request.BackgroundColor ?? section.BackgroundColor;
        section.BannerImageUrl = request.BannerImageUrl ?? section.BannerImageUrl;

        var updated = await _sectionService.UpdateSectionAsync(section, GetUserId());
        
        return Ok(new ApiResponse<HomepageSectionDto>
        {
            Success = true,
            Data = MapToDto(updated),
            Message = "Section updated successfully"
        });
    }

    /// <summary>
    /// Deletes a homepage section (Admin only)
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteSection(int id)
    {
        var section = await _sectionService.GetSectionByIdAsync(id);
        if (section == null)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Section not found" });

        await _sectionService.DeleteSectionAsync(id);
        
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Section deleted successfully"
        });
    }

    // ==================== PRODUCT MANAGEMENT ====================

    /// <summary>
    /// Gets all products in a section
    /// </summary>
    [HttpGet("{sectionId}/products")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetSectionProducts(int sectionId)
    {
        var products = await _sectionService.GetSectionProductsAsync(sectionId);
        var dtos = products.Select(p => new { p.Id, p.Title, p.BasePrice, p.Price }).ToList();
        
        return Ok(new ApiResponse<List<object>>
        {
            Success = true,
            Data = dtos.Cast<object>().ToList(),
            Message = $"Retrieved {dtos.Count} products"
        });
    }

    /// <summary>
    /// Adds a product to a section (Admin only)
    /// </summary>
    [HttpPost("{sectionId}/products")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> AddProductToSection(int sectionId, [FromBody] AddProductToSectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid input" });

        try
        {
            await _sectionService.AddProductToSectionAsync(sectionId, request.ProductId, request.DisplayOrder, request.PromotionalText, request.BadgeText);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Product added to section successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding product to section");
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Removes a product from a section (Admin only)
    /// </summary>
    [HttpDelete("{sectionId}/products/{productId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveProductFromSection(int sectionId, int productId)
    {
        await _sectionService.RemoveProductFromSectionAsync(sectionId, productId);
        
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Product removed from section successfully"
        });
    }

    /// <summary>
    /// Reorders products in a section (Admin only)
    /// </summary>
    [HttpPut("{sectionId}/products/{productId}/order")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> ReorderProduct(int sectionId, int productId, [FromBody] ReorderProductRequest request)
    {
        try
        {
            await _sectionService.UpdateProductDisplayOrderAsync(sectionId, productId, request.NewOrder);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Product reordered successfully"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    // ==================== CATEGORY MANAGEMENT ====================

    /// <summary>
    /// Gets all categories in a section
    /// </summary>
    [HttpGet("{sectionId}/categories")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<object>>>> GetSectionCategories(int sectionId)
    {
        var categories = await _sectionService.GetSectionCategoriesAsync(sectionId);
        var dtos = categories.Select(c => new { c.Id, c.Name }).ToList();
        
        return Ok(new ApiResponse<List<object>>
        {
            Success = true,
            Data = dtos.Cast<object>().ToList(),
            Message = $"Retrieved {dtos.Count} categories"
        });
    }

    /// <summary>
    /// Adds a category to a section (Admin only)
    /// </summary>
    [HttpPost("{sectionId}/categories")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> AddCategoryToSection(int sectionId, [FromBody] AddCategoryToSectionRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(new ApiResponse<object> { Success = false, Message = "Invalid input" });

        try
        {
            await _sectionService.AddCategoryToSectionAsync(sectionId, request.CategoryId, request.DisplayOrder, request.ProductCountToShow);
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Category added to section successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding category to section");
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Removes a category from a section (Admin only)
    /// </summary>
    [HttpDelete("{sectionId}/categories/{categoryId}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveCategoryFromSection(int sectionId, int categoryId)
    {
        await _sectionService.RemoveCategoryFromSectionAsync(sectionId, categoryId);
        
        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Category removed from section successfully"
        });
    }

    // ==================== AUTOMATION CONTROL ====================

    /// <summary>
    /// Enables automated selection for a section (Admin only)
    /// </summary>
    [HttpPost("{id}/enable-automation")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> EnableAutomation(int id)
    {
        var success = await _sectionService.EnableAutomationAsync(id, GetUserId());
        if (!success)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Section not found" });

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Automation enabled successfully"
        });
    }

    /// <summary>
    /// Disables automated selection for a section (Admin only)
    /// </summary>
    [HttpPost("{id}/disable-automation")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> DisableAutomation(int id)
    {
        var success = await _sectionService.DisableAutomationAsync(id, GetUserId());
        if (!success)
            return NotFound(new ApiResponse<object> { Success = false, Message = "Section not found" });

        return Ok(new ApiResponse<object>
        {
            Success = true,
            Message = "Automation disabled successfully"
        });
    }

    /// <summary>
    /// Updates layout configuration (Admin only)
    /// </summary>
    [HttpPut("{id}/layout")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateLayout(int id, [FromBody] UpdateLayoutRequest request)
    {
        try
        {
            await _sectionService.UpdateLayoutConfigurationAsync(id, request.LayoutType, request.ProductsPerRow, request.CardSize, GetUserId());
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Layout updated successfully"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    /// <summary>
    /// Updates display options (Admin only)
    /// </summary>
    [HttpPut("{id}/display-options")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateDisplayOptions(int id, [FromBody] UpdateDisplayOptionsRequest request)
    {
        try
        {
            await _sectionService.UpdateDisplayOptionsAsync(id, request.ShowRating, request.ShowPrice, request.ShowDiscount, GetUserId());
            
            return Ok(new ApiResponse<object>
            {
                Success = true,
                Message = "Display options updated successfully"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new ApiResponse<object> { Success = false, Message = ex.Message });
        }
    }

    // ==================== HELPERS ====================

    private HomepageSectionDto MapToDto(HomepageSection section)
    {
        return new HomepageSectionDto
        {
            Id = section.Id,
            Name = section.Name ?? string.Empty,
            Slug = section.Slug ?? string.Empty,
            DisplayTitle = section.DisplayTitle ?? string.Empty,
            Description = section.Description,
            SectionType = section.SectionType ?? string.Empty,
            DisplayOrder = section.DisplayOrder,
            IsActive = section.IsActive,
            MaxProductsToDisplay = section.MaxProductsToDisplay,
            ProductsPerRow = section.ProductsPerRow,
            LayoutType = section.LayoutType,
            CardSize = section.CardSize,
            UseAutomatedSelection = section.UseAutomatedSelection,
            UseManualSelection = section.UseManualSelection,
            ShowRating = section.ShowRating,
            ShowPrice = section.ShowPrice,
            ShowDiscount = section.ShowDiscount,
            BackgroundColor = section.BackgroundColor,
            BannerImageUrl = section.BannerImageUrl,
            CreatedAt = section.CreatedAt,
            UpdatedAt = section.UpdatedAt
        };
    }
}

// ==================== DTOs & Request Models ====================

public class HomepageSectionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string DisplayTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SectionType { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; }
    public int MaxProductsToDisplay { get; set; }
    public int ProductsPerRow { get; set; }
    public string LayoutType { get; set; } = string.Empty;
    public string CardSize { get; set; } = string.Empty;
    public bool UseAutomatedSelection { get; set; }
    public bool UseManualSelection { get; set; }
    public bool ShowRating { get; set; }
    public bool ShowPrice { get; set; }
    public bool ShowDiscount { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BannerImageUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CreateHomepageSectionRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Slug { get; set; }
    public string DisplayTitle { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string SectionType { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool? IsActive { get; set; }
    public int? MaxProductsToDisplay { get; set; }
    public int? ProductsPerRow { get; set; }
    public string? LayoutType { get; set; }
    public string? CardSize { get; set; }
    public bool? UseAutomatedSelection { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BannerImageUrl { get; set; }
    public bool? ShowRating { get; set; }
    public bool? ShowPrice { get; set; }
    public bool? ShowDiscount { get; set; }
}

public class UpdateHomepageSectionRequest
{
    public string? Name { get; set; }
    public string? DisplayTitle { get; set; }
    public string? Description { get; set; }
    public int? DisplayOrder { get; set; }
    public bool? IsActive { get; set; }
    public int? MaxProductsToDisplay { get; set; }
    public int? ProductsPerRow { get; set; }
    public string? LayoutType { get; set; }
    public string? CardSize { get; set; }
    public string? BackgroundColor { get; set; }
    public string? BannerImageUrl { get; set; }
}

public class AddProductToSectionRequest
{
    public int ProductId { get; set; }
    public int DisplayOrder { get; set; }
    public string? PromotionalText { get; set; }
    public string? BadgeText { get; set; }
}

public class ReorderProductRequest
{
    public int NewOrder { get; set; }
}

public class AddCategoryToSectionRequest
{
    public int CategoryId { get; set; }
    public int DisplayOrder { get; set; }
    public int ProductCountToShow { get; set; } = 6;
}

public class UpdateLayoutRequest
{
    public string LayoutType { get; set; } = string.Empty;
    public int ProductsPerRow { get; set; }
    public string CardSize { get; set; } = string.Empty;
}

public class UpdateDisplayOptionsRequest
{
    public bool ShowRating { get; set; }
    public bool ShowPrice { get; set; }
    public bool ShowDiscount { get; set; }
}

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string Message { get; set; } = string.Empty;
}
