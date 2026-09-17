using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Api.Models.ViewModels;
using System.Security.Claims;

namespace Sparkle.Api.Areas.Seller.Controllers;

[Area("Seller")]
[Authorize(Roles = "Seller")]
public class ReviewsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ReviewsController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return RedirectToAction("Setup", "Dashboard");

        // Get seller's products first, then reviews
        var sellerProductIds = await _db.Products
            .Where(p => p.SellerId == seller.Id)
            .Select(p => p.Id)
            .ToListAsync();
        
        var reviews = await _db.Reviews
            .Include(r => r.Product)
            .Where(r => sellerProductIds.Contains(r.ProductId))
            .OrderByDescending(r => r.CreatedAt)
            .Take(50)
            .ToListAsync();

        var stats = new ReviewStats
        {
            TotalReviews = reviews.Count,
            AverageRating = reviews.Any() ? reviews.Average(r => r.Rating) : 0,
            FiveStarCount = reviews.Count(r => r.Rating == 5),
            FourStarCount = reviews.Count(r => r.Rating == 4),
            ThreeStarCount = reviews.Count(r => r.Rating == 3),
            TwoStarCount = reviews.Count(r => r.Rating == 2),
            OneStarCount = reviews.Count(r => r.Rating == 1),
            ResponseRate = 0 // Not available in current Review model
        };

        ViewBag.Stats = stats;
        return View(reviews);
    }
}
