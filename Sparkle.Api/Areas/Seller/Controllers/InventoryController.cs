using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Api.Services;
using System.Security.Claims;

namespace Sparkle.Api.Areas.Seller.Controllers;

/// <summary>
/// Seller inventory and stock management - SELLER ONLY
/// Only sellers can update their own product stock. Admin cannot manage stock directly.
/// </summary>
[Area("Seller")]
[Authorize(Roles = "Seller")]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IStockManagementService _stockService;
    private readonly ISellerAuthorizationService _sellerAuth;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(
        ApplicationDbContext db,
        IStockManagementService stockService,
        ISellerAuthorizationService sellerAuth,
        ILogger<InventoryController> logger)
    {
        _db = db;
        _stockService = stockService;
        _sellerAuth = sellerAuth;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    private async Task<int?> GetCurrentSellerIdAsync()
    {
        var userId = GetUserId();
        return await _sellerAuth.GetCurrentSellerIdAsync(userId);
    }

    /// <summary>
    /// Inventory overview - shows all seller's products with stock levels
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? filter = null, string? search = null)
    {
        var sellerId = await GetCurrentSellerIdAsync();
        if (sellerId == null)
        {
            _logger.LogWarning($"Unauthorized inventory access attempt by user {GetUserId()}");
            return RedirectToAction("Setup", "Dashboard");
        }

        var query = _db.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .Where(p => p.SellerId == sellerId.Value);

        // Apply filters
        query = filter switch
        {
            "low" => query.Where(p => p.Variants.Sum(v => v.Stock) > 0 && p.Variants.Sum(v => v.Stock) <= 10),
            "out" => query.Where(p => p.Variants.Sum(v => v.Stock) == 0),
            "active" => query.Where(p => p.IsActive),
            "inactive" => query.Where(p => !p.IsActive),
            _ => query
        };

        // Search
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Title.Contains(search) || p.Slug.Contains(search));

        var products = await query
            .OrderBy(p => p.Variants.Sum(v => v.Stock)) // Low stock first
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.Slug,
                p.IsActive,
                Thumbnail = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                TotalStock = p.Variants.Sum(v => v.Stock),
                VariantCount = p.Variants.Count,
                Variants = p.Variants.Select(v => new { v.Id, v.Sku, v.Stock, v.Price }).ToList()
            })
            .ToListAsync();

        var totalProducts = products.Count;
        var lowStockCount = products.Count(p => p.TotalStock > 0 && p.TotalStock <= 10);
        var outOfStockCount = products.Count(p => p.TotalStock == 0);

        // Get inventory summary
        var (_, activeProducts, _, _, totalStockValue) =
            await _stockService.GetSellerInventorySummaryAsync(sellerId.Value);

        ViewBag.Products = products;
        ViewBag.TotalProducts = totalProducts;
        ViewBag.LowStockCount = lowStockCount;
        ViewBag.OutOfStockCount = outOfStockCount;
        ViewBag.ActiveProducts = activeProducts;
        ViewBag.TotalStockValue = totalStockValue;
        ViewBag.CurrentFilter = filter;
        ViewBag.SearchQuery = search;

        _logger.LogInformation($"Seller {sellerId} viewed inventory with filter '{filter}'");
        return View();
    }

    /// <summary>
    /// Update stock for a single variant - SELLER ONLY
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStock(int variantId, int newStock)
    {
        var sellerId = await GetCurrentSellerIdAsync();
        if (sellerId == null)
        {
            _logger.LogWarning($"Unauthorized stock update attempt by user {GetUserId()}");
            return Unauthorized();
        }

        // Verify seller owns this variant
        var variant = await _db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId);

        if (variant == null || variant.Product.SellerId != sellerId.Value)
        {
            _logger.LogWarning($"Seller {sellerId} attempted unauthorized stock update for variant {variantId}");
            return NotFound();
        }

        var (success, errorMessage) = await _stockService.UpdateStockAsync(variantId, newStock, sellerId.Value);

        if (!success)
        {
            if (Request.Headers.Accept.Contains("application/json"))
                return Json(new { success = false, message = errorMessage });

            TempData["Error"] = errorMessage;
            return RedirectToAction("Index");
        }

        _logger.LogInformation($"Seller {sellerId} updated stock for variant {variantId} to {newStock}");

        if (Request.Headers.Accept.Contains("application/json"))
            return Json(new { success = true, newStock });

        TempData["Success"] = $"Stock updated to {newStock}";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Bulk update stock for multiple variants - SELLER ONLY
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkUpdateStock(Dictionary<int, int> stockUpdates)
    {
        var sellerId = await GetCurrentSellerIdAsync();
        if (sellerId == null)
        {
            _logger.LogWarning($"Unauthorized bulk stock update attempt by user {GetUserId()}");
            return Unauthorized();
        }

        var (success, errors) = await _stockService.BulkUpdateStockAsync(stockUpdates, sellerId.Value);

        if (!success)
        {
            TempData["Error"] = string.Join("; ", errors);
            _logger.LogWarning($"Bulk stock update failed for seller {sellerId}: {string.Join("; ", errors)}");
            return RedirectToAction("Index");
        }

        _logger.LogInformation($"Seller {sellerId} successfully updated stock for {stockUpdates.Count} variants");
        TempData["Success"] = $"Updated stock for {stockUpdates.Count} variants";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Get low stock alerts for the seller - SELLER ONLY
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> LowStockAlerts()
    {
        var sellerId = await GetCurrentSellerIdAsync();
        if (sellerId == null)
            return Json(new { success = false, message = "Unauthorized" });

        var lowStockItems = await _stockService.GetSellerLowStockProductsAsync(sellerId.Value);

        _logger.LogInformation($"Seller {sellerId} viewed low stock alerts - {lowStockItems.Count} items");

        return Json(new
        {
            success = true,
            items = lowStockItems.Select(item => new
            {
                item.ProductId,
                item.ProductTitle,
                item.VariantId,
                item.VariantSku,
                item.CurrentStock,
                AlertLevel = item.CurrentStock <= 5 ? "critical" : "warning"
            })
        });
    }

    /// <summary>
    /// Get out of stock products for the seller - SELLER ONLY
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> OutOfStockProducts()
    {
        var sellerId = await GetCurrentSellerIdAsync();
        if (sellerId == null)
            return Json(new { success = false, message = "Unauthorized" });

        var outOfStockItems = await _stockService.GetSellerOutOfStockProductsAsync(sellerId.Value);

        _logger.LogInformation($"Seller {sellerId} viewed out of stock products - {outOfStockItems.Count} items");

        return Json(new
        {
            success = true,
            items = outOfStockItems.Select(item => new
            {
                item.ProductId,
                item.ProductTitle,
                item.VariantCount
            })
        });
    }

    /// <summary>
    /// Get inventory summary for dashboard - SELLER ONLY
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Summary()
    {
        var sellerId = await GetCurrentSellerIdAsync();
        if (sellerId == null)
            return Json(new { success = false, message = "Unauthorized" });

        var (totalProducts, activeProducts, outOfStockCount, lowStockCount, totalStockValue) =
            await _stockService.GetSellerInventorySummaryAsync(sellerId.Value);

        return Json(new
        {
            success = true,
            summary = new
            {
                totalProducts,
                activeProducts,
                inactiveProducts = totalProducts - activeProducts,
                outOfStockCount,
                lowStockCount,
                totalStockValue = Math.Round(totalStockValue, 2),
                inStockCount = totalProducts - outOfStockCount
            }
        });
    }
}
