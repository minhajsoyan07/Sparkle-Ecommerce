using Sparkle.Domain.Logistics;
using Sparkle.Domain.Orders;

namespace Sparkle.Infrastructure.Services;

public interface ILogisticsService
{
    // Pickup Management
    Task<PickupRequest> CreatePickupRequestAsync(Order order, int sellerId);
    Task AssignRiderToPickupAsync(int pickupId, int riderId);
    Task UpdatePickupStatusAsync(int pickupId, PickupStatus status, string? notes = null);

    // Hub & Delivery Management
    Task ReceiveAtHubAsync(int hubId, int orderId); // Creates Inventory
    Task<DeliveryAssignment> CreateDeliveryAssignmentAsync(int orderId, int riderId, int sourceHubId);
    Task UpdateDeliveryStatusAsync(int deliveryId, DeliveryStatus status, string? notes = null);
    Task CompleteDeliveryAsync(int deliveryId);
}
