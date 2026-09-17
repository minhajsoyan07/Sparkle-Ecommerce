using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using System.Security.Claims;

namespace Sparkle.Api.Areas.Seller.Controllers;

/// <summary>
/// Seller marketing and promotions management.
/// </summary>
[Area("Seller")]
[Authorize(Roles = "Seller")]
public class MarketingController : Controller
{
    private readonly ApplicationDbContext _db;

    public MarketingController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    private async Task<int?> GetSellerIdAsync()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        return seller?.Id;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return RedirectToAction("Setup", "Dashboard");

        // Get seller's products with discount info
        var products = await _db.Products
            .Include(p => p.Images)
            .Where(p => p.SellerId == sellerId && p.IsActive)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.BasePrice,
                p.DiscountPercent,
                DiscountedPrice = p.BasePrice * (1 - (p.DiscountPercent ?? 0) / 100),
                Thumbnail = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                HasDiscount = p.DiscountPercent > 0
            })
            .ToListAsync();

        ViewBag.Products = products;
        ViewBag.ProductsOnSale = products.Count(p => p.HasDiscount);
        ViewBag.TotalProducts = products.Count;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDiscount(int productId, decimal discountPercent)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.SellerId == sellerId);

        if (product == null)
            return NotFound();

        // Validate discount range
        discountPercent = Math.Clamp(discountPercent, 0, 90);
        product.DiscountPercent = discountPercent;
        
        await _db.SaveChangesAsync();

        if (Request.Headers.Accept.Contains("application/json"))
            return Json(new { success = true, newDiscount = discountPercent });

        TempData["Success"] = discountPercent > 0 
            ? $"Set {discountPercent}% discount on {product.Title}" 
            : $"Removed discount from {product.Title}";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkSetDiscount(List<int> productIds, decimal discountPercent)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        if (productIds == null || !productIds.Any())
        {
            TempData["Error"] = "No products selected";
            return RedirectToAction("Index");
        }

        discountPercent = Math.Clamp(discountPercent, 0, 90);

        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id) && p.SellerId == sellerId)
            .ToListAsync();

        foreach (var product in products)
        {
            product.DiscountPercent = discountPercent;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Applied {discountPercent}% discount to {products.Count} products";
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAllDiscounts()
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        var products = await _db.Products
            .Where(p => p.SellerId == sellerId && p.DiscountPercent > 0)
            .ToListAsync();

        foreach (var product in products)
        {
            product.DiscountPercent = 0;
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Cleared discounts from {products.Count} products";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> FlashSaleProducts()
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Json(new List<object>());

        // Products with >15% discount are considered flash sale
        var flashSaleProducts = await _db.Products
            .Where(p => p.SellerId == sellerId && p.IsActive && p.DiscountPercent >= 15)
            .Select(p => new
            {
                p.Id,
                p.Title,
                p.BasePrice,
                p.DiscountPercent,
                DiscountedPrice = p.BasePrice * (1 - (p.DiscountPercent ?? 0) / 100)
            })
            .ToListAsync();

        return Json(flashSaleProducts);
    }
}
