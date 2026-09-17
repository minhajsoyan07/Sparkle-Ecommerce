using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Hubs;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Support;
using Sparkle.Infrastructure;
using System.Security.Claims;

namespace Sparkle.Api.Controllers;

/// <summary>
/// Controller for user-side chat functionality.
/// Allows users to message sellers before and after purchase.
/// </summary>
[Authorize]
public class ChatController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatController> _logger;
    private readonly IWebHostEnvironment _env;

    public ChatController(
        ApplicationDbContext db,
        IHubContext<ChatHub> hubContext,
        ILogger<ChatController> logger,
        IWebHostEnvironment env)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
        _env = env;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    /// <summary>
    /// User's chat inbox - list of all conversations.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        
        var chats = await _db.Chats
            .AsNoTracking()
            .Include(c => c.Seller)
            .Include(c => c.Product)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(c => c.UserId == userId && c.Status != "Deleted")
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        return View(chats);
    }

    /// <summary>
    /// View a specific chat conversation.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Conversation(int id)
    {
        var userId = GetUserId();
        
        var chat = await _db.Chats
            .Include(c => c.Seller)
            .Include(c => c.Product)
            .Include(c => c.Messages.Where(m => (m.DeletedFor == null || m.DeletedFor != userId)).OrderBy(m => m.SentAt))
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (chat == null)
            return NotFound();

        // Mark messages as read
        var unreadMessages = chat.Messages.Where(m => !m.IsRead && m.IsSeller).ToList();
        foreach (var msg in unreadMessages)
        {
            msg.IsRead = true;
            msg.ReadAt = DateTime.UtcNow;
        }
        chat.UserUnreadCount = 0;
        await _db.SaveChangesAsync();

        // Load all chats for the sidebar
        var allChats = await _db.Chats
            .AsNoTracking()
            .Include(c => c.Seller)
            .Include(c => c.Product)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .Where(c => c.UserId == userId && c.Status != "Deleted")
            .OrderByDescending(c => c.LastMessageAt)
            .ToListAsync();

        ViewBag.AllChats = allChats;
        ViewBag.ActiveChatId = id;

        return View(chat);
    }

    /// <summary>
    /// Start a new chat with a seller from product page. Instantly creates chat and redirects to Conversation.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> StartChat(int sellerId, int? productId)
    {
        var userId = GetUserId();

        // Check if chat already exists
        var existingChat = await _db.Chats
            .FirstOrDefaultAsync(c => c.UserId == userId && c.SellerId == sellerId && c.Status == "Active");

        if (existingChat != null)
        {
            return RedirectToAction("Conversation", new { id = existingChat.Id });
        }

        var seller = await _db.Sellers.FindAsync(sellerId);
        if (seller == null)
            return NotFound();

        // Auto-create blank chat
        var chat = new Chat
        {
            UserId = userId,
            SellerId = sellerId,
            ProductId = productId,
            Status = "Active",
            StartedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
            SellerUnreadCount = 0,
            UserUnreadCount = 0
        };
        _db.Chats.Add(chat);
        await _db.SaveChangesAsync();

        return RedirectToAction("Conversation", new { id = chat.Id });
    }

    /// <summary>

    /// Send a message in an existing chat (fallback for non-SignalR).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendMessage(int chatId, string message, string? attachmentUrl = null, string? attachmentName = null)
    {
        if (string.IsNullOrWhiteSpace(message) && string.IsNullOrEmpty(attachmentUrl))
            return BadRequest();

        var userId = GetUserId();
        var chat = await _db.Chats
            .Include(c => c.Seller)
            .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId);

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
            IsSeller = false,
            Content = (message ?? "").Trim(),
            MessageType = msgType,
            AttachmentUrl = attachmentUrl,
            AttachmentName = attachmentName,
            SentAt = DateTime.UtcNow
        };
        _db.ChatMessages.Add(chatMessage);
        
        chat.LastMessageAt = DateTime.UtcNow;
        chat.SellerUnreadCount++;
        
        await _db.SaveChangesAsync();

        // Notify seller via SignalR
        var user = await _db.Users.FindAsync(userId);
        await _hubContext.Clients.Group($"seller_{chat.SellerId}").SendAsync("NewMessage", new
        {
            ChatId = chatId,
            SenderName = user?.FullName ?? "Customer",
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
        var userId = GetUserId();
        var chat = await _db.Chats.FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId);
        if (chat == null)
            return NotFound();

        var query = _db.ChatMessages
            .AsNoTracking()
            .Include(m => m.Sender)
            .Where(m => m.ChatId == chatId && (m.DeletedFor == null || m.DeletedFor != userId));

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
    /// Get unread message count for user (for badge display).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = GetUserId();
        var count = await _db.Chats
            .Where(c => c.UserId == userId && c.Status == "Active")
            .SumAsync(c => c.UserUnreadCount);

        return Json(new { count });
    }

    /// <summary>
    /// Archive a chat (soft delete).
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveChat(int id)
    {
        var userId = GetUserId();
        var chat = await _db.Chats.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);
        
        if (chat == null)
            return NotFound();

        chat.Status = "Archived";
        await _db.SaveChangesAsync();

        TempData["Success"] = "Chat archived successfully";
        return RedirectToAction("Index");
    }

    /// <summary>
    /// Edit a message (users can edit their own messages).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EditMessage([FromBody] EditMessageRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { error = "Content cannot be empty" });

        var userId = GetUserId();
        var message = await _db.ChatMessages
            .Include(m => m.Chat)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.SenderId == userId);

        if (message == null)
            return NotFound(new { error = "Message not found or you don't have permission to edit it" });

        // Store original content for audit if needed
        var originalContent = message.Content;
        
        // Update message
        message.Content = request.Content.Trim();
        message.IsEdited = true;
        message.EditedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();

        // Notify both participants via SignalR
        await _hubContext.Clients.Group($"user_{message.Chat.UserId}").SendAsync("MessageEdited", message.Id, message.Content);
        await _hubContext.Clients.Group($"seller_{message.Chat.SellerId}").SendAsync("MessageEdited", message.Id, message.Content);

        _logger.LogInformation("Message {MessageId} edited by {UserId}", message.Id, userId);

        return Ok(new { success = true, messageId = message.Id, content = message.Content });
    }

    /// <summary>
    /// Delete a message. Supports "forMe" (hide for sender only) and "forEveryone" (soft delete globally).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> DeleteMessage([FromBody] DeleteMessageRequest request)
    {
        if (request == null)
            return BadRequest(new { error = "Invalid request" });

        var userId = GetUserId();
        var message = await _db.ChatMessages
            .Include(m => m.Chat)
            .FirstOrDefaultAsync(m => m.Id == request.MessageId && m.SenderId == userId);

        if (message == null)
            return NotFound(new { error = "Message not found or you don't have permission to delete it" });

        if (request.Mode == "forEveryone")
        {
            // Global soft-delete — hidden for all participants
            message.IsDeleted = true;
            message.DeletedFor = "everyone";
            message.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Notify other party via SignalR so it disappears in real-time
            var groupName = message.IsSeller
                ? $"user_{message.Chat.UserId}"
                : $"seller_{message.Chat.SellerId}";
            await _hubContext.Clients.Group(groupName).SendAsync("MessageDeleted", message.Id);
        }
        else
        {
            // Delete for me only — sender no longer sees it, recipient still does
            message.DeletedFor = userId;
            message.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        _logger.LogInformation("Message {MessageId} deleted ({Mode}) by {UserId}", message.Id, request.Mode ?? "forMe", userId);
        return Ok(new { success = true });
    }

    /// <summary>
    /// Upload files/images for chat messages.
    /// Max 5 files, 20MB each. Returns URLs for attachment.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(105_000_000)] // ~100MB total (5 × 20MB + overhead)
    public async Task<IActionResult> UploadChatFiles(List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest(new { error = "No files provided" });

        if (files.Count > 5)
            return BadRequest(new { error = "Maximum 5 files allowed at once" });

        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp",
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".zip", ".rar"
        };
        const long maxFileSize = 20 * 1024 * 1024; // 20MB

        var uploadsDir = Path.Combine(_env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "chat");
        Directory.CreateDirectory(uploadsDir);

        var results = new List<object>();

        foreach (var file in files)
        {
            if (file.Length == 0) continue;
            if (file.Length > maxFileSize)
                return BadRequest(new { error = $"File '{file.FileName}' exceeds 20MB limit" });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(ext))
                return BadRequest(new { error = $"File type '{ext}' is not allowed" });

            var isImage = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp" }.Contains(ext);
            var uniqueName = $"chat_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsDir, uniqueName);

            using (var stream = System.IO.File.Create(filePath))
            {
                await file.CopyToAsync(stream);
            }

            results.Add(new
            {
                url = $"/uploads/chat/{uniqueName}",
                name = file.FileName,
                size = file.Length,
                type = isImage ? "Image" : "File"
            });
        }

        return Ok(new { success = true, files = results });
    }
}

public class EditMessageRequest
{
    public int MessageId { get; set; }
    public string Content { get; set; } = string.Empty;
}

public class DeleteMessageRequest
{
    public int MessageId { get; set; }
    /// <summary>
    /// "forMe" = hide only for sender, "forEveryone" = soft-delete globally
    /// </summary>
    public string? Mode { get; set; } = "forMe";
}
