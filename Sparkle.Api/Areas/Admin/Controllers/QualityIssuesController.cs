using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Reviews;
using Sparkle.Domain.Catalog;

namespace Sparkle.Api.Areas.Admin.Controllers;

using System.Security.Claims;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class QualityIssuesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<QualityIssuesController> _logger;

    public QualityIssuesController(ApplicationDbContext db, ILogger<QualityIssuesController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index(QualityIssueStatus? status = null, QualityIssueSeverity? severity = null)
    {
        var query = _db.Set<ProductQualityIssue>()
            .Include(i => i.Product)
            .OrderByDescending(i => i.DetectedAt)
            .AsQueryable();

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);
        else
            query = query.Where(i => i.Status != QualityIssueStatus.Resolved && i.Status != QualityIssueStatus.Ignored);

        if (severity.HasValue)
            query = query.Where(i => i.Severity == severity.Value);

        var issues = await query.ToListAsync();

        ViewBag.CurrentStatus = status;
        ViewBag.CurrentSeverity = severity;

        // Statistics
        var allIssues = await _db.Set<ProductQualityIssue>().ToListAsync();
        ViewBag.OpenCount = allIssues.Count(i => i.Status == QualityIssueStatus.Open);
        ViewBag.CriticalCount = allIssues.Count(i => i.Severity == QualityIssueSeverity.Critical && i.Status != QualityIssueStatus.Resolved);
        
        var approvedReviewRatings = await _db.ProductReviews
            .Where(r => r.Status == "Approved")
            .Select(r => (double?)r.Rating)
            .ToListAsync();
            
        ViewBag.AvgPlatformRating = approvedReviewRatings.Any() ? approvedReviewRatings.Average() ?? 0 : 0;

        return View(issues);
    }

    public async Task<IActionResult> Details(int id)
    {
        var issue = await _db.Set<ProductQualityIssue>()
            .Include(i => i.Product)
            .ThenInclude(p => p.Seller)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (issue == null)
            return NotFound();

        // Fetch the recent approved product reviews separately to avoid model ambiguity
        ViewBag.RecentReviews = await _db.ProductReviews
            .Where(r => r.ProductId == issue.ProductId && r.Status == "Approved")
            .OrderByDescending(r => r.ReviewDate)
            .Take(5)
            .ToListAsync();

        if (issue.Status == QualityIssueStatus.Open)
        {
            issue.Status = QualityIssueStatus.InReview;
            issue.ReviewedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        return View(issue);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, string notes, string resolution)
    {
        var issue = await _db.Set<ProductQualityIssue>().FindAsync(id);
        if (issue == null) return NotFound();

        issue.Status = QualityIssueStatus.Resolved;
        issue.AdminNotes = notes;
        issue.Resolution = resolution;
        issue.ResolvedAt = DateTime.UtcNow;
        issue.ResolvedBy = User.Identity?.Name;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Quality issue resolved";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TakeAction(int id, string action, string notes)
    {
        var issue = await _db.Set<ProductQualityIssue>()
            .Include(i => i.Product)
            .FirstOrDefaultAsync(i => i.Id == id);
        
        if (issue == null) return NotFound();

        issue.ActionTaken = action;
        issue.AdminNotes = notes;

        if (action == "Suspend Product")
        {
            issue.Product.ModerationStatus = ProductModerationStatus.Suspended;
            issue.ActionTaken = "Admin suspended product through quality review";
            issue.SellerNotified = true;
        }
        else if (action == "Warn Seller")
        {
            issue.SellerNotified = true;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Action '{action}' recorded";
        return RedirectToAction(nameof(Details), new { id = issue.Id });
    }
}
