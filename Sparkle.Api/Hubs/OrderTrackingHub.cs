using Microsoft.AspNetCore.SignalR;

namespace Sparkle.Api.Hubs;

public class OrderTrackingHub : Hub
{
    /// <summary>
    /// Customer subscribes to real-time order updates
    /// </summary>
    public async Task SubscribeToOrder(string orderId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"order_{orderId}");
    }

    /// <summary>
    /// Broadcast order status updates
    /// </summary>
    public async Task BroadcastOrderStatus(string orderId, string status, string message)
    {
        await Clients.Group($"order_{orderId}").SendAsync("ReceiveOrderStatus", new
        {
            OrderId = orderId,
            Status = status,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast live delivery location updates
    /// </summary>
    public async Task BroadcastDeliveryLocation(string orderId, double latitude, double longitude, string riderName, int stopsRemaining)
    {
        await Clients.Group($"order_{orderId}").SendAsync("ReceiveDeliveryLocation", new
        {
            OrderId = orderId,
            Location = new
            {
                Latitude = latitude,
                Longitude = longitude
            },
            RiderName = riderName,
            StopsRemaining = stopsRemaining,
            EstimatedArrival = DateTime.UtcNow.AddMinutes(stopsRemaining * 15),
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast delivery time estimate updates
    /// </summary>
    public async Task BroadcastEtaUpdate(string orderId, DateTime estimatedArrival)
    {
        await Clients.Group($"order_{orderId}").SendAsync("ReceiveEtaUpdate", new
        {
            OrderId = orderId,
            EstimatedArrival = estimatedArrival,
            MinutesAway = (estimatedArrival - DateTime.UtcNow).TotalMinutes,
            Timestamp = DateTime.UtcNow
        });
    }
}
