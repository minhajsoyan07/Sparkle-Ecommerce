using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Reviews;
using Sparkle.Domain.Catalog;

using System.Security.Claims;
using Sparkle.Domain.System;

namespace Sparkle.Api.Areas.Admin.Controllers;

/// <summary>
/// Admin controller for review moderation.
/// Allows admins to view, approve, reject, and manage all reviews.
/// </summary>
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ReviewsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(ApplicationDbContext db, ILogger<ReviewsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// View all reviews with moderation options.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? filter = null, string? search = null, int page = 1)
    {
        var query = _db.ProductReviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Product)
            .Include(r => r.Seller)
            .Include(r => r.Images)
            .AsQueryable();

        // Apply filters
        if (filter == "pending")
            query = query.Where(r => r.Status == "Pending" || r.Status == "PendingModeration");
        else if (filter == "approved")
            query = query.Where(r => r.Status == "Approved");
        else if (filter == "rejected")
            query = query.Where(r => r.Status == "Rejected");
        else if (filter == "reported")
            query = query.Where(r => r.ReportCount > 0);

        // Apply search
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(r => 
                r.Comment!.Contains(search) || 
                r.Title!.Contains(search) ||
                r.User!.FullName!.Contains(search) ||
                r.Product!.Title.Contains(search));
        }

        var totalReviews = await query.CountAsync();
        var pageSize = 20;
        var reviews = await query
            .OrderByDescending(r => r.ReviewDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Statistics
        var allReviews = await _db.ProductReviews.ToListAsync();
        ViewBag.TotalReviews = allReviews.Count;
        ViewBag.PendingCount = allReviews.Count(r => r.Status == "Pending" || r.Status == "PendingModeration");
        ViewBag.ApprovedCount = allReviews.Count(r => r.Status == "Approved");
        ViewBag.RejectedCount = allReviews.Count(r => r.Status == "Rejected");
        ViewBag.ReportedCount = allReviews.Count(r => r.ReportCount > 0);
        ViewBag.AverageRating = allReviews.Any() ? allReviews.Average(r => r.Rating) : 0;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalReviews / (double)pageSize);
        ViewBag.CurrentFilter = filter;
        ViewBag.CurrentSearch = search;

        return View(reviews);
    }

    /// <summary>
    /// Approve a review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var review = await _db.ProductReviews.FindAsync(id);
        if (review == null)
            return NotFound();

        review.Status = "Approved";
        review.ApprovedAt = DateTime.UtcNow;
        review.ReportCount = 0; // Clear reports on approval

        await _db.SaveChangesAsync();

        // Check product quality after approval
        await CheckProductQuality(review.ProductId);

        _logger.LogInformation("Admin approved review {ReviewId}", id);

        TempData["Success"] = "Review approved";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Reject a review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reason)
    {
        var review = await _db.ProductReviews.FindAsync(id);
        if (review == null)
            return NotFound();

        review.Status = "Rejected";
        review.RejectionReason = reason;

        await _db.SaveChangesAsync();

        // Check product quality after rejection (rejections might trigger issues too)
        await CheckProductQuality(review.ProductId);

        _logger.LogInformation("Admin rejected review {ReviewId}. Reason: {Reason}", id, reason);

        TempData["Success"] = "Review rejected";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Delete a review permanently.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var review = await _db.ProductReviews
            .Include(r => r.Images)
            .Include(r => r.Votes)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review == null)
            return NotFound();

        // Delete related images and votes
        _db.ReviewImages.RemoveRange(review.Images);
        _db.ReviewVotes.RemoveRange(review.Votes);
        _db.ProductReviews.Remove(review);

        await _db.SaveChangesAsync();

        // Check product quality after deletion
        await CheckProductQuality(review.ProductId);

        _logger.LogInformation("Admin deleted review {ReviewId}", id);

        TempData["Success"] = "Review deleted permanently";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Clear reports on a review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearReports(int id)
    {
        var review = await _db.ProductReviews.FindAsync(id);
        if (review == null)
            return NotFound();

        review.ReportCount = 0;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Reports cleared";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Bulk approve reviews.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApprove(List<int> reviewIds)
    {
        var reviews = await _db.ProductReviews
            .Where(r => reviewIds.Contains(r.Id))
            .ToListAsync();

        foreach (var review in reviews)
        {
            review.Status = "Approved";
            review.ApprovedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        // Check product quality for each affected product
        var productIds = reviews.Select(r => r.ProductId).Distinct();
        foreach (var productId in productIds)
        {
            await CheckProductQuality(productId);
        }

        _logger.LogInformation("Admin bulk approved {Count} reviews", reviews.Count);

        TempData["Success"] = $"{reviews.Count} reviews approved";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// View review detail.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var review = await _db.ProductReviews
            .Include(r => r.User)
            .Include(r => r.Product)
            .ThenInclude(p => p.Seller)
            .Include(r => r.Images)
            .Include(r => r.OrderItem)
            .ThenInclude(oi => oi!.Order)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review == null)
            return NotFound();

        return View(review);
    }

    /// <summary>
    /// Get review analytics as JSON.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Analytics()
    {
        var reviews = await _db.ProductReviews.ToListAsync();
        var last30Days = DateTime.UtcNow.AddDays(-30);

        var analytics = new
        {
            TotalReviews = reviews.Count,
            AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 2) : 0,
            ReviewsLast30Days = reviews.Count(r => r.ReviewDate >= last30Days),
            RatingDistribution = new
            {
                FiveStar = reviews.Count(r => r.Rating == 5),
                FourStar = reviews.Count(r => r.Rating == 4),
                ThreeStar = reviews.Count(r => r.Rating == 3),
                TwoStar = reviews.Count(r => r.Rating == 2),
                OneStar = reviews.Count(r => r.Rating == 1)
            },
            StatusBreakdown = new
            {
                Pending = reviews.Count(r => r.Status == "Pending"),
                Approved = reviews.Count(r => r.Status == "Approved"),
                Rejected = reviews.Count(r => r.Status == "Rejected")
            },
            TopReviewedProducts = reviews
                .GroupBy(r => r.ProductId)
                .Select(g => new { ProductId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToList()
        };

        return Json(analytics);
    }

    /// <summary>
    /// Add or update admin note on a review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int id, string note, bool isVisible = true)
    {
        var review = await _db.ProductReviews.FindAsync(id);
        if (review == null)
            return NotFound();

        review.AdminNote = note?.Trim();
        review.IsAdminNoteVisible = isVisible;

        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin added note to review {ReviewId}. Visible: {IsVisible}", id, isVisible);

        TempData["Success"] = "Admin note updated";
        return RedirectToAction("Details", new { id });
    }

    /// <summary>
    /// Toggle lock status on a review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleLock(int id)
    {
        var review = await _db.ProductReviews.FindAsync(id);
        if (review == null)
            return NotFound();

        review.IsLocked = !review.IsLocked;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin {Action} review {ReviewId}", review.IsLocked ? "locked" : "unlocked", id);

        TempData["Success"] = review.IsLocked ? "Review locked" : "Review unlocked";
        return RedirectToAction("Details", new { id });
    }

    /// <summary>
    /// View edit history for a review.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> EditHistory(int id)
    {
        var review = await _db.ProductReviews
            .Include(r => r.EditHistory.OrderByDescending(h => h.EditedAt))
            .Include(r => r.User)
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (review == null)
            return NotFound();

        return View(review);
    }

    private async Task CheckProductQuality(int productId)
    {
        try
        {
            var product = await _db.Products
                .Include(p => p.Reviews)
                .FirstOrDefaultAsync(p => p.Id == productId);

            if (product == null) return;

            // Get all approved product reviews directly from DB (using ProductReview model)
            var productReviews = await _db.ProductReviews
                .Where(r => r.ProductId == productId)
                .ToListAsync();

            var approvedReviews = productReviews.Where(r => r.Status == "Approved").ToList();
            if (!approvedReviews.Any()) return;

            var avgRating = (decimal)approvedReviews.Average(r => r.Rating);
            var lowRatingCount = approvedReviews.Count(r => r.Rating <= 2.5);
            var reportCount = approvedReviews.Sum(r => r.ReportCount);
            var rejectedCount = productReviews.Count(r => r.Status == "Rejected");

            // Define thresholds
            bool isLowRating = avgRating < 2.5m;
            bool isHighReport = reportCount >= 3;
            bool isMultipleRejections = rejectedCount >= 5;

            if (isLowRating || isHighReport || isMultipleRejections)
            {
                await CreateOrUpdateQualityIssue(product, avgRating, lowRatingCount, reportCount, rejectedCount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking product quality for product {ProductId}", productId);
        }
    }

    private async Task CreateOrUpdateQualityIssue(Product product, decimal avgRating, int lowCount, int reportCount, int rejectedCount)
    {
        // Check if there's an existing open issue
        var issue = await _db.Set<ProductQualityIssue>()
            .FirstOrDefaultAsync(i => i.ProductId == product.Id && 
                                     (i.Status == QualityIssueStatus.Open || i.Status == QualityIssueStatus.InReview));

        bool isNew = false;
        if (issue == null)
        {
            issue = new ProductQualityIssue
            {
                ProductId = product.Id,
                DetectedAt = DateTime.UtcNow,
                Status = QualityIssueStatus.Open
            };
            isNew = true;
        }

        // Update metrics
        var approvedReviews = await _db.ProductReviews
            .Where(r => r.ProductId == product.Id && r.Status == "Approved")
            .ToListAsync();

        issue.CurrentRating = avgRating;
        issue.TotalReviews = approvedReviews.Count;
        issue.LowRatingCount = lowCount;
        issue.ReportCount = reportCount;
        issue.RejectedReviewCount = rejectedCount;

        // Determine Type & Severity
        if (avgRating < 2.0m)
        {
            issue.IssueType = QualityIssueType.CriticalRating;
            issue.Severity = QualityIssueSeverity.Critical;
            
            // Auto-Suspend if not already suspended
            if (product.ModerationStatus != ProductModerationStatus.Suspended)
            {
                product.ModerationStatus = ProductModerationStatus.Suspended;
                issue.AutoSuspended = true;
                issue.ActionTaken = "Auto-suspended due to critical rating (< 2.0)";
            }
        }
        else if (avgRating < 2.5m)
        {
            issue.IssueType = QualityIssueType.LowAverageRating;
            issue.Severity = QualityIssueSeverity.High;
        }
        else if (reportCount >= 3)
        {
            issue.IssueType = QualityIssueType.HighReportCount;
            issue.Severity = QualityIssueSeverity.Medium;
        }
        else
        {
            issue.IssueType = QualityIssueType.MultipleRejections;
            issue.Severity = QualityIssueSeverity.Low;
        }

        if (isNew)
        {
            _db.Set<ProductQualityIssue>().Add(issue);
        }
        
        await _db.SaveChangesAsync();
    }
}
