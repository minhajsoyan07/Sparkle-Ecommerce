namespace Sparkle.Infrastructure.Services;

public interface INotificationService
{
    Task NotifyUserAsync(string userId, string title, string message, string type = "info", string? actionUrl = null);
    Task NotifySellerAsync(int sellerId, string title, string message, string type = "info", string? actionUrl = null);
}
