using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Sellers;

namespace Sparkle.Api.Controllers;

public class SellerController : Controller
{
    private readonly ApplicationDbContext _db;

    public SellerController(ApplicationDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    [Route("shop/{id}")]
    public async Task<IActionResult> Index(int id, string? q, string? category, string? sort = "newest")
    {
        var seller = await _db.Sellers
            .FirstOrDefaultAsync(s => s.Id == id && s.Status == SellerStatus.Approved);

        if (seller == null)
        {
            return NotFound();
        }

        var productQuery = _db.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Category)
            .Where(p => p.SellerId == id && p.IsActive)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            productQuery = productQuery.Where(p =>
                p.Title.Contains(term) ||
                (p.ShortDescription != null && p.ShortDescription.Contains(term)) ||
                (p.Description != null && p.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            var categoryKey = category.Trim();
            productQuery = productQuery.Where(p =>
                p.Category.Slug == categoryKey ||
                p.Category.Name == categoryKey);
        }

        productQuery = sort switch
        {
            "price_asc" => productQuery.OrderBy(p => p.DiscountPercent.HasValue
                ? p.BasePrice * (1 - (p.DiscountPercent.Value / 100m))
                : p.BasePrice),
            "price_desc" => productQuery.OrderByDescending(p => p.DiscountPercent.HasValue
                ? p.BasePrice * (1 - (p.DiscountPercent.Value / 100m))
                : p.BasePrice),
            "rating" => productQuery.OrderByDescending(p => p.AverageRating).ThenByDescending(p => p.TotalReviews),
            "oldest" => productQuery.OrderBy(p => p.CreatedAt),
            _ => productQuery.OrderByDescending(p => p.CreatedAt)
        };

        var products = await productQuery.ToListAsync();
        var categories = await _db.Categories
            .Where(c => c.Products.Any(p => p.SellerId == id && p.IsActive))
            .OrderBy(c => c.Name)
            .ToListAsync();

        ViewBag.Products = products;
        ViewBag.Categories = categories;
        ViewBag.Query = q ?? string.Empty;
        ViewBag.Category = category ?? string.Empty;
        ViewBag.Sort = sort ?? "newest";
        return View(seller);
    }
}
