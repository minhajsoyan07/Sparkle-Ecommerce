using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Logistics;
using Sparkle.Domain.Orders;

namespace Sparkle.Infrastructure.Services;

public class LogisticsService : ILogisticsService
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notificationService;
    private readonly IWalletService _walletService;

    public LogisticsService(ApplicationDbContext db, INotificationService notificationService, IWalletService walletService)
    {
        _db = db;
        _notificationService = notificationService;
        _walletService = walletService;
    }

    public async Task<PickupRequest> CreatePickupRequestAsync(Order order, int sellerId)
    {
        // Check if exists
        var existing = await _db.PickupRequests
            .FirstOrDefaultAsync(p => p.OrderId == order.Id && p.SellerId == sellerId && !p.IsDeleted);
            
        if (existing != null) return existing;

        var seller = await _db.Sellers.FindAsync(sellerId);
        if (seller == null) throw new InvalidOperationException("Seller not found");

        var pickup = new PickupRequest
        {
            PickupNumber = $"PKP-{order.OrderNumber}-{DateTime.UtcNow.Ticks.ToString()[^6..]}",
            OrderId = order.Id,
            SellerId = sellerId,
            Status = PickupStatus.Scheduled,
            ScheduledAt = DateTime.UtcNow.AddHours(24),
            PickupAddress = seller.BusinessAddress ?? "Unknown Address",
            PickupPhone = seller.MobileNumber ?? "N/A",
            CreatedAt = DateTime.UtcNow
        };

        _db.PickupRequests.Add(pickup);
        await _db.SaveChangesAsync();
        
        return pickup;
    }

    public async Task AssignRiderToPickupAsync(int pickupId, int riderId)
    {
        var pickup = await _db.PickupRequests.FindAsync(pickupId);
        if (pickup == null) throw new ArgumentException("Pickup not found");

        var rider = await _db.Riders.FindAsync(riderId);
        if (rider == null) throw new ArgumentException("Rider not found");

        pickup.AssignedRiderId = riderId;
        pickup.AssignedAt = DateTime.UtcNow;
        pickup.Status = PickupStatus.Assigned;
        pickup.UpdatedAt = DateTime.UtcNow;

        // Notification could go here
        
        await _db.SaveChangesAsync();
    }

    public async Task UpdatePickupStatusAsync(int pickupId, PickupStatus status, string? notes = null)
    {
        var pickup = await _db.PickupRequests.FindAsync(pickupId);
        if (pickup == null) throw new ArgumentException("Pickup not found");

        pickup.Status = status;
        pickup.Notes = notes;
        pickup.UpdatedAt = DateTime.UtcNow;

        if (status == PickupStatus.InProgress)
        {
            pickup.PickedUpAt = DateTime.UtcNow;
        }
        else if (status == PickupStatus.Completed)
        {
            pickup.DeliveredToHubAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task ReceiveAtHubAsync(int hubId, int orderId)
    {
        var existing = await _db.Set<HubInventory>()
            .FirstOrDefaultAsync(h => h.HubId == hubId && h.OrderId == orderId && !h.IsDeleted);
            
        if (existing != null) return; // Already in inventory

        var inventory = new HubInventory
        {
            HubId = hubId,
            OrderId = orderId,
            Status = "Received",
            ReceivedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            QCPassed = true // Auto pass for now (simplified)
        };

        _db.Set<HubInventory>().Add(inventory);
        await _db.SaveChangesAsync();
    }

    public async Task<DeliveryAssignment> CreateDeliveryAssignmentAsync(int orderId, int riderId, int sourceHubId)
    {
        var assignment = new DeliveryAssignment
        {
            DeliveryNumber = $"DEL-{orderId}-{DateTime.UtcNow.Ticks.ToString()[^6..]}",
            OrderId = orderId,
            RiderId = riderId,
            SourceHubId = sourceHubId,
            Status = DeliveryStatus.Assigned,
            AssignedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        _db.DeliveryAssignments.Add(assignment);
        
        // Update Inventory Status
        var inventory = await _db.Set<HubInventory>()
            .FirstOrDefaultAsync(h => h.OrderId == orderId && h.HubId == sourceHubId && !h.IsDeleted);
            
        if (inventory != null)
        {
            inventory.Status = "Dispatched";
            inventory.DispatchedAt = DateTime.UtcNow;
            inventory.DispatchedTo = riderId.ToString();
            inventory.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        return assignment;
    }

    public async Task UpdateDeliveryStatusAsync(int deliveryId, DeliveryStatus status, string? notes = null)
    {
        var delivery = await _db.DeliveryAssignments.FindAsync(deliveryId);
        if (delivery == null) throw new ArgumentException("Delivery not found");

        delivery.Status = status;
        delivery.Notes = notes;
        delivery.UpdatedAt = DateTime.UtcNow;

        if (status == DeliveryStatus.PickedFromHub)
        {
            delivery.PickedFromHubAt = DateTime.UtcNow;
        }
        else if (status == DeliveryStatus.Delivered)
        {
            delivery.DeliveredAt = DateTime.UtcNow;
            
            // Should also update Order Status?
            // The Caller (Controller) typically handles the cross-domain logic or we inject Order logic here.
            // For now, keep it simple, Controller orchestrates.
        }

        await _db.SaveChangesAsync();
    }

    public async Task CompleteDeliveryAsync(int deliveryId)
    {
        await UpdateDeliveryStatusAsync(deliveryId, DeliveryStatus.Delivered, "Delivered to customer");

        // Update Order Status and Release Funds
        var delivery = await _db.DeliveryAssignments
            .Include(d => d.Order)
            .FirstOrDefaultAsync(d => d.Id == deliveryId);

        if (delivery != null && delivery.Order != null)
        {
            delivery.Order.Status = OrderStatus.Delivered;
            delivery.Order.DeliveredAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // Release funds to seller
            await _walletService.ClearPendingToAvailableAsync(delivery.Order.Id);
        }
    }
}
