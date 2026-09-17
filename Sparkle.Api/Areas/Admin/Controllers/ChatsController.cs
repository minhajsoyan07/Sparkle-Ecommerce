using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

/// <summary>
/// Admin controller for viewing chat logs.
/// Allows admins to monitor conversations for policy compliance.
/// </summary>
[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ChatsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ChatsController> _logger;

    public ChatsController(ApplicationDbContext db, ILogger<ChatsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// View all chats with search and filtering.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? search = null, string? status = null, int page = 1)
    {
        var query = _db.Chats
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Seller)
            .Include(c => c.Product)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .AsQueryable();

        // Apply filters
        if (!string.IsNullOrEmpty(status))
            query = query.Where(c => c.Status == status);

        // Apply search
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c => 
                c.User!.FullName!.Contains(search) || 
                c.Seller!.ShopName!.Contains(search) ||
                c.Messages.Any(m => m.Content.Contains(search)));
        }

        var totalChats = await query.CountAsync();
        var pageSize = 20;
        var chats = await query
            .OrderByDescending(c => c.LastMessageAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Statistics
        var allChats = await _db.Chats.ToListAsync();
        ViewBag.TotalChats = allChats.Count;
        ViewBag.ActiveChats = allChats.Count(c => c.Status == "Active");
        ViewBag.ClosedChats = allChats.Count(c => c.Status == "Closed");
        ViewBag.TotalMessages = await _db.ChatMessages.CountAsync();
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalChats / (double)pageSize);
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentSearch = search;

        return View(chats);
    }

    /// <summary>
    /// View a specific chat's messages.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var chat = await _db.Chats
            .Include(c => c.User)
            .Include(c => c.Seller)
            .Include(c => c.Product)
            .Include(c => c.Messages.OrderBy(m => m.SentAt))
            .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chat == null)
            return NotFound();

        return View(chat);
    }

    /// <summary>
    /// Close a chat (admin moderation).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(int id, string? reason)
    {
        var chat = await _db.Chats.FindAsync(id);
        if (chat == null)
            return NotFound();

        chat.Status = "Closed";

        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin closed chat {ChatId}. Reason: {Reason}", id, reason);

        TempData["Success"] = "Chat closed";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Delete a chat and all messages.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var chat = await _db.Chats
            .Include(c => c.Messages)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (chat == null)
            return NotFound();

        _db.ChatMessages.RemoveRange(chat.Messages);
        _db.Chats.Remove(chat);

        await _db.SaveChangesAsync();

        _logger.LogInformation("Admin deleted chat {ChatId}", id);

        TempData["Success"] = "Chat deleted";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Get chat analytics as JSON.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Analytics()
    {
        var chats = await _db.Chats.Include(c => c.Messages).ToListAsync();
        var last30Days = DateTime.UtcNow.AddDays(-30);

        var analytics = new
        {
            TotalChats = chats.Count,
            ActiveChats = chats.Count(c => c.Status == "Active"),
            TotalMessages = chats.Sum(c => c.Messages.Count),
            ChatsLast30Days = chats.Count(c => c.StartedAt >= last30Days),
            AverageMessagesPerChat = chats.Any() ? Math.Round(chats.Average(c => c.Messages.Count), 1) : 0,
            TopSellers = chats
                .GroupBy(c => c.SellerId)
                .Select(g => new { SellerId = g.Key, ChatCount = g.Count() })
                .OrderByDescending(x => x.ChatCount)
                .Take(5)
                .ToList()
        };

        return Json(analytics);
    }
}
