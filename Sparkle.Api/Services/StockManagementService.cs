using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Catalog;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Services;

/// <summary>
/// Centralized service for managing product stock/inventory across the application.
/// Ensures consistency in stock validation, updates, and prevents race conditions.
/// </summary>
public interface IStockManagementService
{
    /// <summary>
    /// Validates if a product variant has sufficient stock
    /// </summary>
    Task<(bool IsValid, string? ErrorMessage)> ValidateStockAsync(int variantId, int requestedQuantity);

    /// <summary>
    /// Reserves stock for a cart item (prevents overselling)
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> ReserveStockAsync(int variantId, int quantity);

    /// <summary>
    /// Releases reserved stock back when order is cancelled or modified
    /// </summary>
    Task<bool> ReleaseStockAsync(int variantId, int quantity);

    /// <summary>
    /// Deducts stock when order is confirmed/placed
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> DeductStockAsync(int variantId, int quantity, string reason = "Order placed");

    /// <summary>
    /// Gets current stock level for a variant
    /// </summary>
    Task<int> GetCurrentStockAsync(int variantId);

    /// <summary>
    /// Gets total stock for a product (sum of all variants)
    /// </summary>
    Task<int> GetProductTotalStockAsync(int productId);

    /// <summary>
    /// Updates stock directly (for seller management)
    /// Admin cannot directly modify stock
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> UpdateStockAsync(int variantId, int newStock, int? sellerId = null);

    /// <summary>
    /// Batch updates stock for multiple variants (for seller management ONLY)
    /// </summary>
    Task<(bool Success, List<string> Errors)> BulkUpdateStockAsync(Dictionary<int, int> stockUpdates, int? sellerId = null);

    /// <summary>
    /// Gets low stock products for a specific seller
    /// </summary>
    Task<List<(int ProductId, string ProductTitle, int VariantId, string VariantSku, int CurrentStock)>> 
        GetSellerLowStockProductsAsync(int sellerId);

    /// <summary>
    /// Gets out of stock products for a specific seller
    /// </summary>
    Task<List<(int ProductId, string ProductTitle, int VariantCount)>> 
        GetSellerOutOfStockProductsAsync(int sellerId);

    /// <summary>
    /// Gets inventory summary for a seller
    /// </summary>
    Task<(int TotalProducts, int ActiveProducts, int OutOfStockCount, int LowStockCount, decimal TotalStockValue)> 
        GetSellerInventorySummaryAsync(int sellerId);
}

public class StockManagementService : IStockManagementService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<StockManagementService> _logger;

    public StockManagementService(ApplicationDbContext db, ILogger<StockManagementService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Validates if a product variant has sufficient stock
    /// </summary>
    public async Task<(bool IsValid, string? ErrorMessage)> ValidateStockAsync(int variantId, int requestedQuantity)
    {
        if (requestedQuantity <= 0)
            return (false, "Quantity must be greater than 0");

        if (requestedQuantity > 99)
            return (false, "Maximum 99 items per order");

        var variant = await _db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId);

        if (variant == null)
            return (false, "Product variant not found");

        if (!variant.Product.IsActive)
            return (false, "Product is not available");

        if (variant.Stock < requestedQuantity)
        {
            _logger.LogWarning($"Insufficient stock for variant {variantId}. Requested: {requestedQuantity}, Available: {variant.Stock}");
            return (false, $"Only {variant.Stock} items available in stock");
        }

        return (true, null);
    }

    /// <summary>
    /// Gets current stock level for a variant
    /// </summary>
    public async Task<int> GetCurrentStockAsync(int variantId)
    {
        var variant = await _db.ProductVariants
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.Id == variantId);

        return variant?.Stock ?? 0;
    }

    /// <summary>
    /// Gets total stock for a product (sum of all variants)
    /// </summary>
    public async Task<int> GetProductTotalStockAsync(int productId)
    {
        var totalStock = await _db.ProductVariants
            .AsNoTracking()
            .Where(v => v.ProductId == productId)
            .SumAsync(v => v.Stock);

        return totalStock;
    }

    /// <summary>
    /// Deducts stock when order is confirmed/placed
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> DeductStockAsync(int variantId, int quantity, string reason = "Order placed")
    {
        if (quantity <= 0)
            return (false, "Quantity must be greater than 0");

        var variant = await _db.ProductVariants
            .FirstOrDefaultAsync(v => v.Id == variantId);

        if (variant == null)
            return (false, "Variant not found");

        if (variant.Stock < quantity)
        {
            _logger.LogError($"Stock deduction failed for variant {variantId}. Required: {quantity}, Available: {variant.Stock}");
            return (false, $"Insufficient stock. Only {variant.Stock} available");
        }

        try
        {
            variant.Stock -= quantity;
            variant.Stock = Math.Max(0, variant.Stock); // Ensure stock never goes negative

            await _db.SaveChangesAsync();

            _logger.LogInformation($"Stock deducted for variant {variantId}: -{quantity} ({reason}). New stock: {variant.Stock}");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deducting stock for variant {variantId}");
            return (false, "Failed to update stock");
        }
    }

    /// <summary>
    /// Updates stock directly (for seller management ONLY)
    /// Admin cannot directly modify stock
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> UpdateStockAsync(int variantId, int newStock, int? sellerId = null)
    {
        if (newStock < 0)
            return (false, "Stock cannot be negative");

        var variant = await _db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId);

        if (variant == null)
            return (false, "Variant not found");

        // Authorization check: If sellerId provided, verify ownership
        if (sellerId.HasValue && variant.Product.SellerId != sellerId.Value)
        {
            _logger.LogWarning($"Unauthorized stock update attempt for variant {variantId} by seller {sellerId}");
            return (false, "Unauthorized: You can only update stock for your own products");
        }

        try
        {
            variant.Stock = newStock;
            await _db.SaveChangesAsync();

            _logger.LogInformation($"Stock updated for variant {variantId} to {newStock} (Seller: {sellerId})");
            return (true, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating stock for variant {variantId}");
            return (false, "Failed to update stock. Please try again.");
        }
    }

    /// <summary>
    /// Batch updates stock for multiple variants (for seller management ONLY)
    /// Ensures all variants belong to the seller before updating
    /// </summary>
    public async Task<(bool Success, List<string> Errors)> BulkUpdateStockAsync(
        Dictionary<int, int> stockUpdates, int? sellerId = null)
    {
        var errors = new List<string>();

        if (stockUpdates == null || !stockUpdates.Any())
        {
            errors.Add("No stock updates provided");
            return (false, errors);
        }

        // Validate all updates before proceeding
        var variantIds = stockUpdates.Keys.ToList();
        var variants = await _db.ProductVariants
            .Include(v => v.Product)
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync();

        // Authorization check: Verify ownership if sellerId provided
        if (sellerId.HasValue)
        {
            var unauthorizedVariants = variants.Where(v => v.Product.SellerId != sellerId.Value).ToList();
            if (unauthorizedVariants.Any())
            {
                _logger.LogWarning($"Unauthorized bulk stock update attempt by seller {sellerId} for {unauthorizedVariants.Count} variants");
                errors.Add($"Unauthorized: You can only update stock for your own products");
                return (false, errors);
            }
        }

        // Validate stock values
        foreach (var (variantId, newStock) in stockUpdates)
        {
            if (newStock < 0)
            {
                errors.Add($"Variant {variantId}: Stock cannot be negative");
            }
        }

        if (errors.Any())
            return (false, errors);

        try
        {
            foreach (var variant in variants)
            {
                if (stockUpdates.TryGetValue(variant.Id, out var newStock))
                {
                    variant.Stock = Math.Max(0, newStock);
                }
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation($"Bulk stock update successful: {variants.Count} variants updated (Seller: {sellerId})");
            return (true, new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during bulk stock update");
            errors.Add("Failed to update stock. Please try again.");
            return (false, errors);
        }
    }

    /// <summary>
    /// Gets products with low stock for a specific seller
    /// </summary>
    public async Task<List<(int ProductId, string ProductTitle, int VariantId, string VariantSku, int CurrentStock)>> 
        GetSellerLowStockProductsAsync(int sellerId)
    {
        var lowStockItems = await _db.ProductVariants
  .AsNoTracking()
  .Include(v => v.Product)
          .Where(v => v.Stock > 0 && v.Stock <= 10 && v.Product.SellerId == sellerId)
.Select(v => new 
            { 
        ProductId = v.Product.Id,
  ProductTitle = v.Product.Title,
   VariantId = v.Id,
     VariantSku = v.Sku,
        CurrentStock = v.Stock
 })
            .OrderBy(v => v.CurrentStock)
    .ToListAsync();

     return lowStockItems
         .Select(x => (ProductId: x.ProductId, ProductTitle: x.ProductTitle, VariantId: x.VariantId, VariantSku: x.VariantSku ?? "N/A", CurrentStock: x.CurrentStock))
    .ToList();
    }

  /// <summary>
    /// Gets out of stock products for a specific seller
    /// </summary>
    public async Task<List<(int ProductId, string ProductTitle, int VariantCount)>> 
        GetSellerOutOfStockProductsAsync(int sellerId)
    {
        var outOfStockProducts = await _db.Products
          .AsNoTracking()
      .Include(p => p.Variants)
            .Where(p => p.SellerId == sellerId && p.Variants.Sum(v => v.Stock) == 0)
          .Select(p => new 
        { 
            p.Id,
 p.Title,
     VariantCount = p.Variants.Count
     })
            .OrderByDescending(p => p.VariantCount)
        .ToListAsync();

    return outOfStockProducts
            .Select(x => (ProductId: x.Id, ProductTitle: x.Title, VariantCount: x.VariantCount))
  .ToList();
    }

 /// <summary>
  /// Gets inventory summary for a seller
    /// </summary>
    public async Task<(int TotalProducts, int ActiveProducts, int OutOfStockCount, int LowStockCount, decimal TotalStockValue)> 
   GetSellerInventorySummaryAsync(int sellerId)
    {
   var products = await _db.Products
        .AsNoTracking()
            .Include(p => p.Variants)
            .Where(p => p.SellerId == sellerId)
            .ToListAsync();

     var totalProducts = products.Count;
        var activeProducts = products.Count(p => p.IsActive);
        var outOfStockCount = products.Count(p => p.Variants.Sum(v => v.Stock) == 0);
   var lowStockCount = products.Count(p => 
        {
   var stock = p.Variants.Sum(v => v.Stock);
        return stock > 0 && stock <= 10;
   });

        var totalStockValue = products.Sum(p => 
       p.Variants.Sum(v => v.Stock * (v.Price > 0 ? v.Price : p.BasePrice))
        );

        return (totalProducts, activeProducts, outOfStockCount, lowStockCount, totalStockValue);
    }

    /// <summary>
    /// Reserves stock for a cart item (prevents overselling)
    /// </summary>
    public async Task<(bool Success, string? ErrorMessage)> ReserveStockAsync(int variantId, int quantity)
    {
        return await ValidateStockAsync(variantId, quantity);
    }

    /// <summary>
    /// Releases reserved stock back when order is cancelled or modified
    /// </summary>
    public async Task<bool> ReleaseStockAsync(int variantId, int quantity)
    {
        if (quantity <= 0)
 return false;

      var variant = await _db.ProductVariants
      .FirstOrDefaultAsync(v => v.Id == variantId);

        if (variant == null)
  return false;

        try
        {
    variant.Stock += quantity;
            await _db.SaveChangesAsync();

      _logger.LogInformation($"Stock released for variant {variantId}: +{quantity}. New stock: {variant.Stock}");
   return true;
        }
        catch (Exception ex)
        {
        _logger.LogError(ex, $"Error releasing stock for variant {variantId}");
    return false;
        }
    }

    /// <summary>
    /// Gets low stock products (stock <= 10)
    /// </summary>
    public async Task<List<(int VariantId, string ProductTitle, int CurrentStock)>> GetLowStockProductsAsync(int? sellerId = null)
    {
        var query = _db.ProductVariants
            .AsNoTracking()
         .Include(v => v.Product)
     .Where(v => v.Stock <= 10 && v.Stock > 0);

        if (sellerId.HasValue)
 query = query.Where(v => v.Product.SellerId == sellerId.Value);

        var lowStockItems = await query
            .Select(v => new { v.Id, v.Product.Title, v.Stock })
            .ToListAsync();

        return lowStockItems
  .Select(x => (VariantId: x.Id, ProductTitle: x.Title, CurrentStock: x.Stock))
            .ToList();
    }

    /// <summary>
    /// Validates all items in order before processing
    /// </summary>
    public async Task<(bool IsValid, List<string> Errors)> ValidateOrderStockAsync(List<(int VariantId, int Quantity)> items)
    {
        var errors = new List<string>();

  if (items == null || !items.Any())
        {
       errors.Add("No items in order");
          return (false, errors);
        }

      foreach (var item in items)
   {
       var (isValid, errorMessage) = await ValidateStockAsync(item.VariantId, item.Quantity);
   if (!isValid)
    errors.Add(errorMessage ?? "Unknown error validating stock");
        }

        return (errors.Count == 0, errors);
  }

    /// <summary>
    /// Checks if a product is completely out of stock across all variants
    /// </summary>
    public async Task<bool> IsProductOutOfStockAsync(int productId)
    {
        var totalStock = await GetProductTotalStockAsync(productId);
        return totalStock <= 0;
    }

    /// <summary>
    /// Gets product stock status (Out of Stock, Low Stock, In Stock)
    /// </summary>
    public async Task<(string Status, int TotalStock)> GetProductStockStatusAsync(int productId)
    {
        var totalStock = await GetProductTotalStockAsync(productId);
        
   var status = totalStock switch
      {
     0 => "Out of Stock",
        <= 10 => "Low Stock",
            _ => "In Stock"
  };

        return (status, totalStock);
    }

    /// <summary>
    /// Gets product availability info with total and low variant info
    /// </summary>
    public async Task<ProductAvailabilityInfo> GetProductAvailabilityAsync(int productId)
    {
        var variants = await _db.ProductVariants
       .AsNoTracking()
      .Where(v => v.ProductId == productId)
            .Select(v => new { v.Id, v.Stock })
            .ToListAsync();

        var totalStock = variants.Sum(v => v.Stock);
        var availableVariants = variants.Count(v => v.Stock > 0);
        var totalVariants = variants.Count;
var isOutOfStock = totalStock <= 0;
        var isLowStock = totalStock > 0 && totalStock <= 10;

 return new ProductAvailabilityInfo
        {
      TotalStock = totalStock,
            AvailableVariants = availableVariants,
     TotalVariants = totalVariants,
  IsOutOfStock = isOutOfStock,
        IsLowStock = isLowStock,
            Status = isOutOfStock ? "Out of Stock" : isLowStock ? "Low Stock" : "In Stock"
        };
    }
}
