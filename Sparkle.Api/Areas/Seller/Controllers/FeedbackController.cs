using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Reviews;
using Sparkle.Infrastructure;
using System.Security.Claims;

namespace Sparkle.Api.Areas.Seller.Controllers;

/// <summary>
/// Unified controller for seller feedback and review management.
/// Combines product reviews with advanced filtering, sorting, and moderation features.
/// </summary>
[Area("Seller")]
[Authorize(Roles = "Seller")]
public class FeedbackController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<FeedbackController> _logger;

    public FeedbackController(ApplicationDbContext db, ILogger<FeedbackController> logger)
    {
        _db = db;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    private async Task<int?> GetSellerIdAsync()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        return seller?.Id;
    }

    /// <summary>
    /// Unified feedback page with product filtering, sorting, and advanced features.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(
        string? tab = "reviews", 
        string? filter = null, 
        int? productId = null,
        string? sort = "latest",
        int page = 1)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return RedirectToAction("Login", "Auth", new { area = "" });

        ViewBag.ActiveTab = tab ?? "reviews";

        if (tab == "reviews")
        {
            // Base query for seller's reviews
            var query = _db.ProductReviews
                .AsNoTracking()
                .Include(r => r.User)
                .Include(r => r.Product)
                .ThenInclude(p => p.Images)
                .Include(r => r.Images)
                .Where(r => r.Product.SellerId == sellerId);

            // Product filter
            if (productId.HasValue && productId > 0)
                query = query.Where(r => r.ProductId == productId);

            // Quick filters
            query = filter switch
            {
                "pending" => query.Where(r => string.IsNullOrEmpty(r.SellerResponse)),
                "responded" => query.Where(r => !string.IsNullOrEmpty(r.SellerResponse)),
                "positive" => query.Where(r => r.Rating >= 4),
                "negative" => query.Where(r => r.Rating <= 2),
                "pinned" => query.Where(r => r.IsPinned),
                "reported" => query.Where(r => r.ReportCount > 0),
                _ => query
            };

            // Sorting
            query = sort switch
            {
                "highest" => query.OrderByDescending(r => r.Rating).ThenByDescending(r => r.ReviewDate),
                "lowest" => query.OrderBy(r => r.Rating).ThenByDescending(r => r.ReviewDate),
                "edited" => query.OrderByDescending(r => r.EditCount).ThenByDescending(r => r.ReviewDate),
                "helpful" => query.OrderByDescending(r => r.HelpfulCount).ThenByDescending(r => r.ReviewDate),
                _ => query.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.ReviewDate) // Latest, pinned first
            };

            var totalReviews = await query.CountAsync();
            var pageSize = 10;
            var reviews = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Statistics
            var allReviews = await _db.ProductReviews
                .Include(r => r.Product)
                .Where(r => r.Product.SellerId == sellerId)
                .ToListAsync();

            // Get seller products for filter dropdown
            var sellerProducts = await _db.Products
                .Where(p => p.SellerId == sellerId && p.IsActive)
                .Select(p => new { p.Id, p.Title })
                .OrderBy(p => p.Title)
                .ToListAsync();

            ViewBag.TotalReviews = allReviews.Count;
            ViewBag.AverageRating = allReviews.Any() ? allReviews.Average(r => r.Rating) : 0;
            ViewBag.PendingResponses = allReviews.Count(r => string.IsNullOrEmpty(r.SellerResponse));
            ViewBag.PositiveReviews = allReviews.Count(r => r.Rating >= 4);
            ViewBag.NegativeReviews = allReviews.Count(r => r.Rating <= 2);
            ViewBag.PinnedReviews = allReviews.Count(r => r.IsPinned);
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalReviews / (double)pageSize);
            ViewBag.CurrentFilter = filter;
            ViewBag.CurrentSort = sort;
            ViewBag.CurrentProductId = productId;
            ViewBag.Reviews = reviews;
            ViewBag.SellerProducts = sellerProducts;
        }

        return View();
    }

    /// <summary>
    /// Get seller products for filter dropdown (AJAX).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetProducts(string? search)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Json(new List<object>());

        var query = _db.Products
            .Where(p => p.SellerId == sellerId && p.IsActive);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Title.Contains(search));

        var products = await query
            .Select(p => new { p.Id, p.Title })
            .Take(20)
            .ToListAsync();

        return Json(products);
    }

    /// <summary>
    /// View a single review detail.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return RedirectToAction("Login", "Auth", new { area = "" });

        var review = await _db.ProductReviews
            .Include(r => r.User)
            .Include(r => r.Product)
            .ThenInclude(p => p.Images)
            .Include(r => r.Images)
            .Include(r => r.OrderItem)
            .ThenInclude(oi => oi!.Order)
            .Include(r => r.EditHistory.OrderByDescending(h => h.EditedAt).Take(5))
            .FirstOrDefaultAsync(r => r.Id == id && r.Product.SellerId == sellerId);

        if (review == null)
            return NotFound();

        return View(review);
    }

    /// <summary>
    /// Add or update seller response to a review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Respond(int reviewId, string response)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        if (string.IsNullOrWhiteSpace(response))
        {
            TempData["Error"] = "Response cannot be empty";
            return RedirectToAction("Details", new { id = reviewId });
        }

        var review = await _db.ProductReviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.Product.SellerId == sellerId);

        if (review == null)
            return NotFound();

        // Check if locked
        if (review.IsLocked)
        {
            TempData["Error"] = "This review is locked by admin and cannot be modified";
            return RedirectToAction("Details", new { id = reviewId });
        }

        // Track edit history if updating existing response
        if (!string.IsNullOrEmpty(review.SellerResponse))
        {
            _db.ReviewEditHistories.Add(new ReviewEditHistory
            {
                ProductReviewId = reviewId,
                PreviousComment = review.SellerResponse,
                NewComment = response.Trim(),
                EditedAt = DateTime.UtcNow,
                EditedBy = GetUserId(),
                EditType = "SellerResponse"
            });
        }

        review.SellerResponse = response.Trim();
        review.SellerResponseDate = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Seller {SellerId} responded to review {ReviewId}", sellerId, reviewId);

        TempData["Success"] = "Response submitted successfully";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Delete seller response.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResponse(int reviewId)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        var review = await _db.ProductReviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.Product.SellerId == sellerId);

        if (review == null)
            return NotFound();

        if (review.IsLocked)
        {
            TempData["Error"] = "This review is locked and cannot be modified";
            return RedirectToAction("Index");
        }

        review.SellerResponse = null;
        review.SellerResponseDate = null;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Response deleted";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Toggle pin status for a review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePin(int reviewId)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        var review = await _db.ProductReviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.Product.SellerId == sellerId);

        if (review == null)
            return NotFound();

        review.IsPinned = !review.IsPinned;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Seller {SellerId} {Action} review {ReviewId}", 
            sellerId, review.IsPinned ? "pinned" : "unpinned", reviewId);

        return Json(new { success = true, isPinned = review.IsPinned });
    }

    /// <summary>
    /// Request admin review for suspicious/fake feedback.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestAdminReview(int reviewId, string reason)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        var review = await _db.ProductReviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.Product.SellerId == sellerId);

        if (review == null)
            return NotFound();

        // Increment report count which flags for admin attention
        review.ReportCount++;
        
        // Log the request
        _db.ReviewEditHistories.Add(new ReviewEditHistory
        {
            ProductReviewId = reviewId,
            EditedAt = DateTime.UtcNow,
            EditedBy = GetUserId(),
            EditType = "AdminRequest",
            NewComment = reason
        });

        await _db.SaveChangesAsync();

        _logger.LogInformation("Seller {SellerId} requested admin review for review {ReviewId}. Reason: {Reason}", 
            sellerId, reviewId, reason);

        TempData["Success"] = "Admin review requested. The review will be examined shortly.";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Get review statistics as JSON (for dashboard widget).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Stats()
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Json(new { });

        var reviews = await _db.ProductReviews
            .Include(r => r.Product)
            .Where(r => r.Product.SellerId == sellerId)
            .ToListAsync();

        var stats = new
        {
            TotalReviews = reviews.Count,
            AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0,
            FiveStarCount = reviews.Count(r => r.Rating == 5),
            FourStarCount = reviews.Count(r => r.Rating == 4),
            ThreeStarCount = reviews.Count(r => r.Rating == 3),
            TwoStarCount = reviews.Count(r => r.Rating == 2),
            OneStarCount = reviews.Count(r => r.Rating == 1),
            PendingResponses = reviews.Count(r => string.IsNullOrEmpty(r.SellerResponse)),
            PinnedCount = reviews.Count(r => r.IsPinned),
            RecentReviews = reviews
                .OrderByDescending(r => r.ReviewDate)
                .Take(5)
                .Select(r => new
                {
                    r.Id,
                    r.Rating,
                    Comment = r.Comment?.Length > 50 ? r.Comment.Substring(0, 50) + "..." : r.Comment,
                    Date = r.ReviewDate.ToString("MMM d"),
                    r.IsPinned
                })
                .ToList()
        };

        return Json(stats);
    }
}
