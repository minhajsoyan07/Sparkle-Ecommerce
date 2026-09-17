using Microsoft.AspNetCore.SignalR;

namespace Sparkle.Api.Hubs;

public class NotificationHub : Hub
{
    /// <summary>
    /// Send notification to specific user
    /// </summary>
    public async Task SendNotificationToUser(string userId, string title, string message, string type)
    {
        await Clients.User(userId).SendAsync("ReceiveNotification", new
        {
            Title = title,
            Message = message,
            Type = type, // info, success, warning, error
            Timestamp = DateTime.UtcNow,
            IsRead = false
        });
    }

    /// <summary>
    /// Broadcast price drop alert to interested users
    /// </summary>
    public async Task BroadcastPriceDropAlert(int productId, decimal oldPrice, decimal newPrice)
    {
        await Clients.Group($"watchlist_{productId}").SendAsync("ReceivePriceDropAlert", new
        {
            ProductId = productId,
            OldPrice = oldPrice,
            NewPrice = newPrice,
            DiscountPercentage = Math.Round(((oldPrice - newPrice) / oldPrice) * 100, 2),
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Subscribe to product price alerts (wishlist items)
    /// </summary>
    public async Task SubscribeToPriceAlerts(int productId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"watchlist_{productId}");
    }

    /// <summary>
    /// Broadcast flash sale start notification
    /// </summary>
    public async Task BroadcastFlashSaleStart(int productId, string productName, decimal salePrice, DateTime endTime)
    {
        await Clients.All.SendAsync("ReceiveFlashSaleAlert", new
        {
            ProductId = productId,
            ProductName = productName,
            SalePrice = salePrice,
            EndTime = endTime,
            Message = $"Flash Sale Alert! {productName} now at ৳{salePrice}",
            Timestamp = DateTime.UtcNow
        });
    }
}
