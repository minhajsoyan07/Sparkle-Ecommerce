using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Content;
using Sparkle.Domain.Catalog;
using Sparkle.Infrastructure;
using System.Security.Claims;

namespace Sparkle.Api.Services;

/// <summary>
/// Manages homepage sections, their configurations, and product selections
/// Handles both manual and automated product selection for sections
/// </summary>
public interface IHomepageSectionService
{
    // Section Management
    Task<HomepageSection?> GetSectionBySlugAsync(string slug);
    Task<List<HomepageSection>> GetAllActiveSectionsAsync();
    Task<HomepageSection?> GetSectionByIdAsync(int sectionId);
    Task<HomepageSection> CreateSectionAsync(HomepageSection section, string? userId);
    Task<HomepageSection> UpdateSectionAsync(HomepageSection section, string? userId);
    Task DeleteSectionAsync(int sectionId);

    // Product Management in Sections
    Task<List<Product>> GetSectionProductsAsync(int sectionId, int? limit = null);
    Task AddProductToSectionAsync(int sectionId, int productId, int displayOrder, string? promotionalText = null, string? badgeText = null);
    Task RemoveProductFromSectionAsync(int sectionId, int productId);
    Task UpdateProductDisplayOrderAsync(int sectionId, int productId, int newOrder);
    Task<HomepageSectionProduct?> GetSectionProductAsync(int sectionId, int productId);

    // Category Management (for category shop sections)
    Task<List<Category>> GetSectionCategoriesAsync(int sectionId);
    Task AddCategoryToSectionAsync(int sectionId, int categoryId, int displayOrder, int productCountToShow);
    Task RemoveCategoryFromSectionAsync(int sectionId, int categoryId);
    Task<List<Product>> GetCategoryProductsForSectionAsync(int sectionId, int categoryId);

    // Automation Control
    Task<bool> EnableAutomationAsync(int sectionId, string? userId);
    Task<bool> DisableAutomationAsync(int sectionId, string? userId);
    Task<bool> SetManualSelectionAsync(int sectionId, bool useManual, string? userId);

    // Section Display Configuration
    Task UpdateLayoutConfigurationAsync(int sectionId, string layoutType, int productsPerRow, string cardSize, string? userId);
    Task UpdateDisplayOptionsAsync(int sectionId, bool showRating, bool showPrice, bool showDiscount, string? userId);
}

public class HomepageSectionService : IHomepageSectionService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<HomepageSectionService> _logger;

    public HomepageSectionService(ApplicationDbContext db, ILogger<HomepageSectionService> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ==================== SECTION MANAGEMENT ====================

    /// <summary>
    /// Gets a section by its unique slug
    /// </summary>
    public async Task<HomepageSection?> GetSectionBySlugAsync(string slug)
    {
        return await _db.HomepageSections
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Slug == slug && s.IsActive);
    }

    /// <summary>
    /// Gets all active homepage sections ordered by display order
    /// </summary>
    public async Task<List<HomepageSection>> GetAllActiveSectionsAsync()
    {
        return await _db.HomepageSections
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();
    }

    /// <summary>
    /// Gets a section by ID with all related data
    /// </summary>
    public async Task<HomepageSection?> GetSectionByIdAsync(int sectionId)
    {
        return await _db.HomepageSections
            .Include(s => s.ManualProducts)
            .Include(s => s.RelatedCategories)
            .FirstOrDefaultAsync(s => s.Id == sectionId);
    }

    /// <summary>
    /// Creates a new homepage section
    /// </summary>
    public async Task<HomepageSection> CreateSectionAsync(HomepageSection section, string? userId)
    {
        section.CreatedByUserId = userId;
        section.CreatedAt = DateTime.UtcNow;
        section.UpdatedAt = DateTime.UtcNow;

        _db.HomepageSections.Add(section);
        await _db.SaveChangesAsync();

        // Log the creation
        await LogAuditAsync(section.Id, "Created", "Section", null, section.Name, userId, isAutomated: false);

        _logger.LogInformation($"Homepage section '{section.Name}' created by {userId}");
        return section;
    }

    /// <summary>
    /// Updates an existing homepage section
    /// </summary>
    public async Task<HomepageSection> UpdateSectionAsync(HomepageSection section, string? userId)
    {
        var existing = await _db.HomepageSections.FindAsync(section.Id);
        if (existing == null)
            throw new InvalidOperationException($"Section {section.Id} not found");

        // Track changes for audit log
        if (existing.Name != section.Name)
            await LogAuditAsync(section.Id, "Updated", "Name", existing.Name, section.Name, userId);
        if (existing.DisplayOrder != section.DisplayOrder)
            await LogAuditAsync(section.Id, "Updated", "DisplayOrder", existing.DisplayOrder.ToString(), section.DisplayOrder.ToString(), userId);
        if (existing.IsActive != section.IsActive)
            await LogAuditAsync(section.Id, "Updated", "IsActive", existing.IsActive.ToString(), section.IsActive.ToString(), userId);

        existing.Name = section.Name;
        existing.DisplayTitle = section.DisplayTitle;
        existing.Description = section.Description;
        existing.DisplayOrder = section.DisplayOrder;
        existing.IsActive = section.IsActive;
        existing.MaxProductsToDisplay = section.MaxProductsToDisplay;
        existing.ProductsPerRow = section.ProductsPerRow;
        existing.LayoutType = section.LayoutType;
        existing.CardSize = section.CardSize;
        existing.ShowRating = section.ShowRating;
        existing.ShowPrice = section.ShowPrice;
        existing.ShowDiscount = section.ShowDiscount;
        existing.BackgroundColor = section.BackgroundColor;
        existing.BannerImageUrl = section.BannerImageUrl;
        existing.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        _logger.LogInformation($"Homepage section '{section.Name}' updated by {userId}");

        return existing;
    }

    /// <summary>
    /// Deletes a homepage section and all its associated data
    /// </summary>
    public async Task DeleteSectionAsync(int sectionId)
    {
        var section = await _db.HomepageSections
            .Include(s => s.ManualProducts)
            .Include(s => s.RelatedCategories)
            .Include(s => s.AuditLogs)
            .FirstOrDefaultAsync(s => s.Id == sectionId);

        if (section == null)
            throw new InvalidOperationException($"Section {sectionId} not found");

        _db.HomepageSections.Remove(section);
        await _db.SaveChangesAsync();

        _logger.LogInformation($"Homepage section '{section.Name}' deleted");
    }

    // ==================== PRODUCT MANAGEMENT ====================

    /// <summary>
    /// Gets all products assigned to a section
    /// Filters to only show in-stock, active products
    /// </summary>
    public async Task<List<Product>> GetSectionProductsAsync(int sectionId, int? limit = null)
    {
        var query = _db.HomepageSectionProducts
            .AsNoTracking()
            .Where(hp => hp.SectionId == sectionId && hp.IsActive)
            .OrderBy(hp => hp.DisplayOrder)
            .Select(hp => hp.Product)
            .Where(p => p.IsActive);

        var products = await query.ToListAsync();

        // Filter products to only include those with stock
        var inStockProducts = new List<Product>();
        foreach (var product in products)
        {
            var totalStock = await _db.ProductVariants
                .AsNoTracking()
                .Where(v => v.ProductId == product.Id)
                .SumAsync(v => v.Stock);

            if (totalStock > 0)
            {
                inStockProducts.Add(product);

                if (limit.HasValue && inStockProducts.Count >= limit.Value)
                    break;
            }
        }

        return inStockProducts;
    }

    /// <summary>
    /// Adds a product to a section
    /// </summary>
    public async Task AddProductToSectionAsync(int sectionId, int productId, int displayOrder, string? promotionalText = null, string? badgeText = null)
    {
        var section = await _db.HomepageSections.FindAsync(sectionId);
        if (section == null)
            throw new InvalidOperationException($"Section {sectionId} not found");

        var product = await _db.Products.FindAsync(productId);
        if (product == null)
            throw new InvalidOperationException($"Product {productId} not found");

        // Check if product already exists in section
        var existing = await _db.HomepageSectionProducts
            .FirstOrDefaultAsync(hp => hp.SectionId == sectionId && hp.ProductId == productId);

        if (existing != null)
        {
            existing.IsActive = true;
            existing.DisplayOrder = displayOrder;
            existing.PromotionalText = promotionalText;
            existing.BadgeText = badgeText;
        }
        else
        {
            var sectionProduct = new HomepageSectionProduct
            {
                SectionId = sectionId,
                ProductId = productId,
                DisplayOrder = displayOrder,
                PromotionalText = promotionalText,
                BadgeText = badgeText,
                IsActive = true
            };
            _db.HomepageSectionProducts.Add(sectionProduct);
        }

        await _db.SaveChangesAsync();
        await LogAuditAsync(sectionId, "ProductAdded", "ProductId", null, productId.ToString(), null);

        _logger.LogInformation($"Product {productId} added to section {sectionId}");
    }

    /// <summary>
    /// Removes a product from a section
    /// </summary>
    public async Task RemoveProductFromSectionAsync(int sectionId, int productId)
    {
        var sectionProduct = await _db.HomepageSectionProducts
            .FirstOrDefaultAsync(hp => hp.SectionId == sectionId && hp.ProductId == productId);

        if (sectionProduct != null)
        {
            _db.HomepageSectionProducts.Remove(sectionProduct);
            await _db.SaveChangesAsync();
            await LogAuditAsync(sectionId, "ProductRemoved", "ProductId", productId.ToString(), null, null);

            _logger.LogInformation($"Product {productId} removed from section {sectionId}");
        }
    }

    /// <summary>
    /// Updates the display order of a product in a section
    /// </summary>
    public async Task UpdateProductDisplayOrderAsync(int sectionId, int productId, int newOrder)
    {
        var sectionProduct = await _db.HomepageSectionProducts
            .FirstOrDefaultAsync(hp => hp.SectionId == sectionId && hp.ProductId == productId);

        if (sectionProduct == null)
            throw new InvalidOperationException($"Product {productId} not found in section {sectionId}");

        sectionProduct.DisplayOrder = newOrder;
        await _db.SaveChangesAsync();

        _logger.LogInformation($"Product {productId} display order updated to {newOrder} in section {sectionId}");
    }

    /// <summary>
    /// Gets details of a product in a section
    /// </summary>
    public async Task<HomepageSectionProduct?> GetSectionProductAsync(int sectionId, int productId)
    {
        return await _db.HomepageSectionProducts
            .AsNoTracking()
            .FirstOrDefaultAsync(hp => hp.SectionId == sectionId && hp.ProductId == productId);
    }

    // ==================== CATEGORY MANAGEMENT ====================

    /// <summary>
    /// Gets all categories assigned to a section
    /// </summary>
    public async Task<List<Category>> GetSectionCategoriesAsync(int sectionId)
    {
        return await _db.HomepageSectionCategories
            .AsNoTracking()
            .Where(hc => hc.SectionId == sectionId && hc.IsActive)
            .OrderBy(hc => hc.DisplayOrder)
            .Select(hc => hc.Category)
            .ToListAsync();
    }

    /// <summary>
    /// Adds a category to a section
    /// </summary>
    public async Task AddCategoryToSectionAsync(int sectionId, int categoryId, int displayOrder, int productCountToShow)
    {
        var section = await _db.HomepageSections.FindAsync(sectionId);
        if (section == null)
            throw new InvalidOperationException($"Section {sectionId} not found");

        var category = await _db.Categories.FindAsync(categoryId);
        if (category == null)
            throw new InvalidOperationException($"Category {categoryId} not found");

        var existing = await _db.HomepageSectionCategories
            .FirstOrDefaultAsync(hc => hc.SectionId == sectionId && hc.CategoryId == categoryId);

        if (existing != null)
        {
            existing.IsActive = true;
            existing.DisplayOrder = displayOrder;
            existing.ProductCountToShow = productCountToShow;
        }
        else
        {
            var sectionCategory = new HomepageSectionCategory
            {
                SectionId = sectionId,
                CategoryId = categoryId,
                DisplayOrder = displayOrder,
                ProductCountToShow = productCountToShow,
                IsActive = true
            };
            _db.HomepageSectionCategories.Add(sectionCategory);
        }

        await _db.SaveChangesAsync();
        await LogAuditAsync(sectionId, "CategoryAdded", "CategoryId", null, categoryId.ToString(), null);

        _logger.LogInformation($"Category {categoryId} added to section {sectionId}");
    }

    /// <summary>
    /// Removes a category from a section
    /// </summary>
    public async Task RemoveCategoryFromSectionAsync(int sectionId, int categoryId)
    {
        var sectionCategory = await _db.HomepageSectionCategories
            .FirstOrDefaultAsync(hc => hc.SectionId == sectionId && hc.CategoryId == categoryId);

        if (sectionCategory != null)
        {
            _db.HomepageSectionCategories.Remove(sectionCategory);
            await _db.SaveChangesAsync();
            await LogAuditAsync(sectionId, "CategoryRemoved", "CategoryId", categoryId.ToString(), null, null);

            _logger.LogInformation($"Category {categoryId} removed from section {sectionId}");
        }
    }

    /// <summary>
    /// Gets products from a specific category in a section
    /// Only returns in-stock products
    /// </summary>
    public async Task<List<Product>> GetCategoryProductsForSectionAsync(int sectionId, int categoryId)
    {
        var sectionCategory = await _db.HomepageSectionCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(hc => hc.SectionId == sectionId && hc.CategoryId == categoryId);

        if (sectionCategory == null)
            return new List<Product>();

        var products = await _db.Products
            .AsNoTracking()
            .Where(p => p.CategoryId == categoryId && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        // Filter to only include in-stock products
        var inStockProducts = new List<Product>();
        foreach (var product in products)
        {
            if (inStockProducts.Count >= sectionCategory.ProductCountToShow)
                break;

            var totalStock = await _db.ProductVariants
                .AsNoTracking()
                .Where(v => v.ProductId == product.Id)
                .SumAsync(v => v.Stock);

            if (totalStock > 0)
            {
                inStockProducts.Add(product);
            }
        }

        return inStockProducts;
    }

    // ==================== AUTOMATION CONTROL ====================

    /// <summary>
    /// Enables automated selection for a section
    /// </summary>
    public async Task<bool> EnableAutomationAsync(int sectionId, string? userId)
    {
        var section = await _db.HomepageSections.FindAsync(sectionId);
        if (section == null)
            return false;

        section.UseAutomatedSelection = true;
        section.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogAuditAsync(sectionId, "Updated", "UseAutomatedSelection", "false", "true", userId);
        _logger.LogInformation($"Automated selection enabled for section {sectionId}");

        return true;
    }

    /// <summary>
    /// Disables automated selection for a section
    /// </summary>
    public async Task<bool> DisableAutomationAsync(int sectionId, string? userId)
    {
        var section = await _db.HomepageSections.FindAsync(sectionId);
        if (section == null)
            return false;

        section.UseAutomatedSelection = false;
        section.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogAuditAsync(sectionId, "Updated", "UseAutomatedSelection", "true", "false", userId);
        _logger.LogInformation($"Automated selection disabled for section {sectionId}");

        return true;
    }

    /// <summary>
    /// Sets whether to use manual selection mode
    /// </summary>
    public async Task<bool> SetManualSelectionAsync(int sectionId, bool useManual, string? userId)
    {
        var section = await _db.HomepageSections.FindAsync(sectionId);
        if (section == null)
            return false;

        section.UseManualSelection = useManual;
        section.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        await LogAuditAsync(sectionId, "Updated", "UseManualSelection", (!useManual).ToString(), useManual.ToString(), userId);
        _logger.LogInformation($"Manual selection {(useManual ? "enabled" : "disabled")} for section {sectionId}");

        return true;
    }

    // ==================== DISPLAY CONFIGURATION ====================

    /// <summary>
    /// Updates layout configuration (type, products per row, card size)
    /// </summary>
    public async Task UpdateLayoutConfigurationAsync(int sectionId, string layoutType, int productsPerRow, string cardSize, string? userId)
    {
        var section = await _db.HomepageSections.FindAsync(sectionId);
        if (section == null)
            throw new InvalidOperationException($"Section {sectionId} not found");

        // Validate input
        var validLayouts = new[] { "Grid", "Carousel", "List" };
        var validCardSizes = new[] { "Small", "Medium", "Large" };

        if (!validLayouts.Contains(layoutType))
            throw new InvalidOperationException($"Invalid layout type: {layoutType}");

        if (!validCardSizes.Contains(cardSize))
            throw new InvalidOperationException($"Invalid card size: {cardSize}");

        if (productsPerRow < 1 || productsPerRow > 6)
            throw new InvalidOperationException("Products per row must be between 1 and 6");

        section.LayoutType = layoutType;
        section.ProductsPerRow = productsPerRow;
        section.CardSize = cardSize;
        section.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await LogAuditAsync(sectionId, "Updated", "LayoutType", null, layoutType, userId);
        await LogAuditAsync(sectionId, "Updated", "ProductsPerRow", null, productsPerRow.ToString(), userId);
        await LogAuditAsync(sectionId, "Updated", "CardSize", null, cardSize, userId);

        _logger.LogInformation($"Layout configuration updated for section {sectionId}");
    }

    /// <summary>
    /// Updates display options (show rating, price, discount)
    /// </summary>
    public async Task UpdateDisplayOptionsAsync(int sectionId, bool showRating, bool showPrice, bool showDiscount, string? userId)
    {
        var section = await _db.HomepageSections.FindAsync(sectionId);
        if (section == null)
            throw new InvalidOperationException($"Section {sectionId} not found");

        section.ShowRating = showRating;
        section.ShowPrice = showPrice;
        section.ShowDiscount = showDiscount;
        section.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await LogAuditAsync(sectionId, "Updated", "ShowRating", null, showRating.ToString(), userId);
        await LogAuditAsync(sectionId, "Updated", "ShowPrice", null, showPrice.ToString(), userId);
        await LogAuditAsync(sectionId, "Updated", "ShowDiscount", null, showDiscount.ToString(), userId);

        _logger.LogInformation($"Display options updated for section {sectionId}");
    }

    // ==================== INTERNAL HELPERS ====================

    /// <summary>
    /// Logs changes to sections for audit trail
    /// </summary>
    private async Task LogAuditAsync(int sectionId, string changeType, string propertyName, string? oldValue, string? newValue, string? userId, bool isAutomated = false)
    {
        var auditLog = new HomepageSectionAuditLog
        {
            SectionId = sectionId,
            ChangeType = changeType,
            PropertyName = propertyName,
            OldValue = oldValue,
            NewValue = newValue,
            ChangedByUserId = userId,
            ChangedAt = DateTime.UtcNow,
            IsAutomatedChange = isAutomated
        };

        _db.HomepageSectionAuditLogs.Add(auditLog);
        await _db.SaveChangesAsync();
    }
}
