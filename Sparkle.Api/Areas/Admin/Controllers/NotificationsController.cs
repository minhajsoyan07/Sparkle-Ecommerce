using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Notifications;
using Sparkle.Domain.Sellers;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NotificationsController> _logger;

    public NotificationsController(ApplicationDbContext db, ILogger<NotificationsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // GET: /Admin/Notifications
    public async Task<IActionResult> Index(string? target = null, int page = 1)
    {
        var query = _db.Notifications.AsQueryable();

        if (!string.IsNullOrEmpty(target) && target != "all")
        {
            // Filter by notification type if needed
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * 20)
            .Take(20)
            .ToListAsync();

        var totalCount = await query.CountAsync();
        
        ViewBag.TotalCount = totalCount;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalCount / 20.0);

        return View(notifications);
    }

    // GET: /Admin/Notifications/Create
    public IActionResult Create()
    {
        return View(new NotificationViewModel());
    }

    // POST: /Admin/Notifications/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NotificationViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var targetUserIds = new List<string>();

            // Determine target users based on selection
            if (model.Target == "all")
            {
                targetUserIds = await _db.Users.Select(u => u.Id).ToListAsync();
            }
            else if (model.Target == "sellers")
            {
                // Get active sellers (Status == Active)
                targetUserIds = await _db.Sellers
                    .Where(s => s.Status == SellerStatus.Approved)
                    .Select(s => s.UserId)
                    .ToListAsync();
            }
            else if (model.Target == "users")
            {
                // Get users who are not sellers
                var sellerUserIds = await _db.Sellers.Select(s => s.UserId).ToListAsync();
                targetUserIds = await _db.Users
                    .Where(u => !sellerUserIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();
            }

            // Create notifications for each target user
            var notifications = targetUserIds.Select(userId => new Notification
            {
                UserId = userId,
                Title = model.Title,
                Message = model.Message,
                Type = NotificationType.General,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            if (notifications.Any())
            {
                _db.Notifications.AddRange(notifications);
                await _db.SaveChangesAsync();
            }

            _logger.LogInformation("Sent notification to {Count} users. Title: {Title}", notifications.Count, model.Title);

            TempData["Success"] = $"Notification sent successfully to {notifications.Count} users!";
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending notification");
            TempData["Error"] = "Failed to send notification. Please try again.";
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var notification = await _db.Notifications.FindAsync(id);
        if (notification != null)
        {
            _db.Notifications.Remove(notification);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Notification deleted successfully!";
        }

        return RedirectToAction("Index");
    }

    public class NotificationViewModel
    {
        public string Target { get; set; } = "all"; // all, users, sellers
        public string Title { get; set; } = "";
        public string Message { get; set; } = "";
        public bool SendEmail { get; set; }
        public bool SendInApp { get; set; } = true;
    }
}
