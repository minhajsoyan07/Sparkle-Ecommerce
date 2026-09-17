using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Hubs;
using Sparkle.Infrastructure;
using Sparkle.Infrastructure.Services;
using SystemNotification = Sparkle.Domain.System.Notification;

namespace Sparkle.Api.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationService(ApplicationDbContext db, IHubContext<NotificationHub> hubContext)
    {
        _db = db;
        _hubContext = hubContext;
    }

    public async Task NotifyUserAsync(string userId, string title, string message, string type = "info", string? actionUrl = null)
    {
        // 1. Persist to Database
        var notification = new SystemNotification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            IsRead = false,
        };
        
        _db.SystemNotifications.Add(notification);
        await _db.SaveChangesAsync(); // Save to get ID and timestamp

        // 2. Push via SignalR
        await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", new
        {
            Id = notification.Id,
            Title = title,
            Message = message,
            Type = type,
            ActionUrl = actionUrl,
            Timestamp = DateTime.UtcNow,
            IsRead = false
        });
    }

    public async Task NotifySellerAsync(int sellerId, string title, string message, string type = "info", string? actionUrl = null)
    {
        // Resolve Seller's Owner User ID
        var seller = await _db.Sellers.FindAsync(sellerId);
        if (seller != null && !string.IsNullOrEmpty(seller.UserId))
        {
            await NotifyUserAsync(seller.UserId, title, message, type, actionUrl);
        }
    }
}
