using Microsoft.AspNetCore.SignalR;

namespace Sparkle.Api.Hubs;

public class InventoryHub : Hub
{
    /// <summary>
    /// Client subscribes to inventory updates for specific products
    /// </summary>
    public async Task SubscribeToProduct(int productId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"product_{productId}");
    }

    /// <summary>
    /// Client unsubscribes from product updates
    /// </summary>
    public async Task UnsubscribeFromProduct(int productId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"product_{productId}");
    }

    /// <summary>
    /// Broadcast stock level changes to subscribed clients
    /// Called from backend services when inventory changes
    /// </summary>
    public async Task BroadcastStockUpdate(int productId, int newStockLevel, bool isLowStock)
    {
        await Clients.Group($"product_{productId}").SendAsync("ReceiveStockUpdate", new
        {
            ProductId = productId,
            StockLevel = newStockLevel,
            IsLowStock = isLowStock,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Notify clients about concurrent viewers for social proof
    /// </summary>
    public async Task BroadcastViewerCount(int productId, int viewerCount)
    {
        await Clients.Group($"product_{productId}").SendAsync("ReceiveViewerCount", new
        {
            ProductId = productId,
            ViewerCount = viewerCount,
            Timestamp = DateTime.UtcNow
        });
    }

    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await base.OnDisconnectedAsync(exception);
    }
}
