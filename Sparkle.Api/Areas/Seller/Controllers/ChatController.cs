using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Hubs;
using Sparkle.Domain.Support;
using Sparkle.Infrastructure;
using System.Security.Claims;

namespace Sparkle.Api.Areas.Seller.Controllers;

/// <summary>
/// Controller for seller-side chat functionality.
/// Allows sellers to view and respond to customer messages.
/// </summary>
[Area("Seller")]
[Authorize(Roles = "Seller")]
public class ChatController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        ApplicationDbContext db,
        IHubContext<ChatHub> hubContext,
        ILogger<ChatController> logger)
    {
        _db = db;
        _hubContext = hubContext;
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
    /// Seller's chat inbox - list of all customer conversations.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return RedirectToAction("Login", "Auth", new { area = "" });

        var chats = await _db.Chats
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Product)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(c => c.SellerId == sellerId && c.Status != "Deleted")
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        // Get unread count
        ViewBag.UnreadCount = chats.Sum(c => c.SellerUnreadCount);
        
        return View(chats);
    }

    /// <summary>
    /// View a specific chat conversation.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Conversation(int id)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return RedirectToAction("Login", "Auth", new { area = "" });

        var chat = await _db.Chats
            .Include(c => c.User)
            .Include(c => c.Product)
            .Include(c => c.Messages.OrderBy(m => m.SentAt))
            .ThenInclude(m => m.Sender)
            .FirstOrDefaultAsync(c => c.Id == id && c.SellerId == sellerId);

        if (chat == null)
            return NotFound();

        // Mark messages as read
        var unreadMessages = chat.Messages.Where(m => !m.IsRead && !m.IsSeller).ToList();
        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
            msg.ReadAt = DateTime.UtcNow;
        }
        chat.SellerUnreadCount = 0;
        await _db.SaveChangesAsync();

        return View(chat);
    }

    /// <summary>
    /// Start or resume a chat with a specific user (e.g., from Order Details).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StartChat(string userId, int? productId = null)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return RedirectToAction("Login", "Auth", new { area = "" });

        // Find existing chat
        var existingChat = await _db.Chats
            .FirstOrDefaultAsync(c => c.SellerId == sellerId && c.UserId == userId && (productId == null || c.ProductId == productId));

        if (existingChat != null)
        {
            return RedirectToAction("Conversation", new { id = existingChat.Id });
        }

        // Create new chat
        var newChat = new Chat
        {
            SellerId = sellerId.Value,
            UserId = userId,
            ProductId = productId,
            StartedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            Status = "Active"
        };

        _db.Chats.Add(newChat);
        await _db.SaveChangesAsync();

        return RedirectToAction("Conversation", new { id = newChat.Id });
    }

    /// <summary>
    /// Send a message in an existing chat.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int chatId, string message, string? attachmentUrl = null, string? attachmentName = null)
    {
        if (string.IsNullOrWhiteSpace(message) && string.IsNullOrEmpty(attachmentUrl))
            return BadRequest();

        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        var userId = GetUserId();
        var chat = await _db.Chats
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == chatId && c.SellerId == sellerId);

        if (chat == null)
            return NotFound();

        var msgType = "Text";
        if (!string.IsNullOrEmpty(attachmentUrl))
        {
            var imageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" };
            msgType = imageExts.Any(e => attachmentUrl.EndsWith(e, StringComparison.OrdinalIgnoreCase)) ? "Image" : "File";
        }

        var chatMessage = new ChatMessage
        {
            ChatId = chatId,
            SenderId = userId,
            IsSeller = true,
            Content = (message ?? "").Trim(),
            MessageType = msgType,
            AttachmentUrl = attachmentUrl,
            AttachmentName = attachmentName,
            SentAt = DateTime.UtcNow
        };
        _db.ChatMessages.Add(chatMessage);
        
        chat.LastMessageAt = DateTime.UtcNow;
        chat.UserUnreadCount++;
        
        await _db.SaveChangesAsync();

        // Notify user via SignalR
        var seller = await _db.Sellers.FindAsync(sellerId);
        await _hubContext.Clients.Group($"user_{chat.UserId}").SendAsync("NewMessage", new
        {
            ChatId = chatId,
            SenderName = seller?.ShopName ?? "Seller",
            Preview = message?.Length > 50 ? message[..50] + "..." : (message ?? "Sent an attachment"),
            SentAt = chatMessage.SentAt.ToString("o")
        });

        if (Request.Headers.Accept.Contains("application/json"))
        {
            return Json(new { success = true, messageId = chatMessage.Id });
        }

        return RedirectToAction("Conversation", new { id = chatId });
    }

    /// <summary>
    /// API: Get chat messages for real-time updates.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMessages(int chatId, DateTime? after)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        var chat = await _db.Chats.FirstOrDefaultAsync(c => c.Id == chatId && c.SellerId == sellerId);
        if (chat == null)
            return NotFound();

        var query = _db.ChatMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.ChatId == chatId && (m.DeletedFor == null || m.DeletedFor != sellerId.ToString()));

        if (after.HasValue)
            query = query.Where(m => m.SentAt > after.Value);

        var messages = await query
            .OrderBy(m => m.SentAt)
            .Select(m => new
            {
                m.Id,
                m.SenderId,
                SenderName = m.Sender.FullName ?? "Unknown",
                m.IsSeller,
                m.Content,
                m.MessageType,
                m.AttachmentUrl,
                SentAt = m.SentAt.ToString("o"),
                m.IsRead,
                m.IsEdited,
                m.IsDeleted
            })
            .ToListAsync();

        return Json(messages);
    }

    /// <summary>
    /// Get unread message count for seller (for badge display).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Json(new { count = 0 });

        var count = await _db.Chats
            .Where(c => c.SellerId == sellerId && c.Status == "Active")
            .SumAsync(c => c.SellerUnreadCount);

        return Json(new { count });
    }

    /// <summary>
    /// Close a chat conversation.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseChat(int id)
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Unauthorized();

        var chat = await _db.Chats.FirstOrDefaultAsync(c => c.Id == id && c.SellerId == sellerId);
        
        if (chat == null)
            return NotFound();

        chat.Status = "Closed";
        await _db.SaveChangesAsync();

        TempData["Success"] = "Chat closed successfully";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Get chat list as JSON for dashboard widget.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> RecentChats()
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Json(new List<object>());

        var chats = await _db.Chats
            .AsNoTracking()
            .Include(c => c.User)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(c => c.SellerId == sellerId && c.Status == "Active")
            .OrderByDescending(c => c.LastMessageAt)
            .Take(5)
            .Select(c => new
            {
                c.Id,
                CustomerName = c.User.FullName ?? "Customer",
                LastMessage = c.Messages.Any() ? c.Messages.First().Content : "",
                LastMessageAt = c.LastMessageAt.ToString("o"),
                UnreadCount = c.SellerUnreadCount
            })
            .ToListAsync();

        return Json(chats);
    }
}
