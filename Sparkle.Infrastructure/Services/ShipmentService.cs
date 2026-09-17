using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Sellers;

namespace Sparkle.Infrastructure.Services;

/// <summary>
/// Service for managing shipments and tracking
/// </summary>
public class ShipmentService
{
    private readonly ApplicationDbContext _context;
    private readonly IWalletService _walletService;

    public ShipmentService(ApplicationDbContext context, IWalletService walletService)
    {
        _context = context;
        _walletService = walletService;
    }

    /// <summary>
    /// Create a shipment for an order (or part of an order for multi-vendor)
    /// </summary>
    public async Task<Shipment> CreateShipmentAsync(
        int orderId, 
        int sellerId, 
        List<int> orderItemIds,
        string courierName,
        string? trackingNumber = null)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            throw new Exception("Order not found");

        // Generate shipment number
        var shipmentNumber = await GenerateShipmentNumberAsync();

        // Create shipment
        var shipment = new Shipment
        {
            ShipmentNumber = shipmentNumber,
            OrderId = orderId,
            SellerId = sellerId,
            CourierName = courierName,
            TrackingNumber = trackingNumber,
            Status = ShipmentStatus.Pending,
            
            // Copy shipping address from order
            RecipientName = order.ShippingFullName,
            RecipientPhone = order.ShippingPhone,
            ShippingAddress = $"{order.ShippingAddressLine1}, {order.ShippingAddressLine2}".Trim(new[] { ',', ' ' }),
            ShippingCity = order.ShippingCity,
            ShippingDistrict = order.ShippingDistrict,
            ShippingPostalCode = order.ShippingPostalCode,
            ShippingCost = order.ShippingCost,
            
            CreatedAt = DateTime.UtcNow
        };

        _context.Shipments.Add(shipment);
        await _context.SaveChangesAsync();

        // Add shipment items
        foreach (var orderItemId in orderItemIds)
        {
            var orderItem = order.OrderItems.FirstOrDefault(oi => oi.Id == orderItemId);
            if (orderItem != null)
            {
                var shipmentItem = new ShipmentItem
                {
                    ShipmentId = shipment.Id,
                    OrderItemId = orderItemId,
                    Quantity = orderItem.Quantity,
                    ProductSKU = orderItem.ProductSKU,
                    ProductName = orderItem.ProductName,
                    VariantName = orderItem.VariantName,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ShipmentItems.Add(shipmentItem);
            }
        }

        await _context.SaveChangesAsync();

        // Add initial tracking event
        await AddTrackingEventAsync(
            shipment.Id,
            ShipmentStatus.Pending,
            "Shipment created and awaiting pickup",
            null,
            "System"
        );

        return shipment;
    }

    /// <summary>
    /// Update shipment status and add tracking event
    /// </summary>
    public async Task<Shipment> UpdateShipmentStatusAsync(
        int shipmentId,
        ShipmentStatus status,
        string message,
        string? location = null,
        string? userId = null)
    {
        var shipment = await _context.Shipments.FindAsync(shipmentId);
        if (shipment == null)
            throw new Exception("Shipment not found");

        shipment.Status = status;
        shipment.StatusMessage = message;
        shipment.UpdatedAt = DateTime.UtcNow;

        // Update specific date fields based on status
        switch (status)
        {
            case ShipmentStatus.Packed:
                shipment.PackedAt = DateTime.UtcNow;
                break;
            case ShipmentStatus.PickedUp:
                shipment.PickedUpAt = DateTime.UtcNow;
                break;
            case ShipmentStatus.InTransit:
                shipment.ShippedAt ??= DateTime.UtcNow;
                break;
            case ShipmentStatus.Delivered:
                shipment.DeliveredAt = DateTime.UtcNow;
                // Release funds to seller
                try
                {
                    await _walletService.ClearPendingToAvailableAsync(shipment.OrderId, shipment.SellerId);
                }
                catch (Exception)
                {
                    // Log error or handle as needed
                }
                break;
            case ShipmentStatus.Cancelled:
                shipment.CancelledAt = DateTime.UtcNow;
                break;
        }

        await _context.SaveChangesAsync();

        // Add tracking event
        await AddTrackingEventAsync(shipmentId, status, message, location, userId ?? "System");

        return shipment;
    }

    /// <summary>
    /// Add a tracking event to shipment
    /// </summary>
    public async Task<ShipmentTrackingEvent> AddTrackingEventAsync(
        int shipmentId,
        ShipmentStatus status,
        string message,
        string? location = null,
        string eventSource = "Webhook",
        string? courierEventData = null)
    {
        var trackingEvent = new ShipmentTrackingEvent
        {
            ShipmentId = shipmentId,
            Status = status.ToString(),
            NormalizedStatus = status,
            Message = message,
            Location = location,
            EventTime = DateTime.UtcNow,
            CourierEventData = courierEventData,
            EventSource = eventSource,
            CustomerNotified = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.ShipmentTrackingEvents.Add(trackingEvent);
        await _context.SaveChangesAsync();

        return trackingEvent;
    }

    /// <summary>
    /// Get shipments for an order
    /// </summary>
    public async Task<List<Shipment>> GetShipmentsByOrderIdAsync(int orderId)
    {
        return await _context.Shipments
            .Include(s => s.Seller)
            .Include(s => s.Items)
                .ThenInclude(si => si.OrderItem)
            .Include(s => s.TrackingEvents.OrderByDescending(te => te.EventTime))
            .Where(s => s.OrderId == orderId)
            .ToListAsync();
    }

    /// <summary>
    /// Get shipments for a seller
    /// </summary>
    public async Task<List<Shipment>> GetShipmentsBySellerIdAsync(int sellerId, int pageNumber = 1, int pageSize = 20)
    {
        return await _context.Shipments
            .Include(s => s.Order)
            .Include(s => s.Items)
            .Where(s => s.SellerId == sellerId)
            .OrderByDescending(s => s.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    /// <summary>
    /// Get shipment by tracking number
    /// </summary>
    public async Task<Shipment?> GetShipmentByTrackingNumberAsync(string trackingNumber)
    {
        return await _context.Shipments
            .Include(s => s.Order)
            .Include(s => s.Seller)
            .Include(s => s.Items)
                .ThenInclude(si => si.OrderItem)
            .Include(s => s.TrackingEvents.OrderByDescending(te => te.EventTime))
            .FirstOrDefaultAsync(s => s.TrackingNumber == trackingNumber);
    }

    /// <summary>
    /// Get tracking timeline for shipment
    /// </summary>
    public async Task<List<ShipmentTrackingEvent>> GetTrackingTimelineAsync(int shipmentId)
    {
        return await _context.ShipmentTrackingEvents
            .Where(te => te.ShipmentId == shipmentId)
            .OrderByDescending(te => te.EventTime)
            .ToListAsync();
    }

    /// <summary>
    /// Update tracking number and courier info
    /// </summary>
    public async Task<Shipment> UpdateTrackingInfoAsync(
        int shipmentId,
        string trackingNumber,
        string? courierShipmentId = null,
        string? trackingUrl = null)
    {
        var shipment = await _context.Shipments.FindAsync(shipmentId);
        if (shipment == null)
            throw new Exception("Shipment not found");

        shipment.TrackingNumber = trackingNumber;
        shipment.CourierShipmentId = courierShipmentId;
        shipment.TrackingUrl = trackingUrl;
        shipment.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return shipment;
    }

    /// <summary>
    /// Generate unique shipment number
    /// </summary>
    private async Task<string> GenerateShipmentNumberAsync()
    {
        var date = DateTime.UtcNow;
        var prefix = $"SHP-{date:yyyyMMdd}";
        
        // Find last shipment number for today
        var lastShipment = await _context.Shipments
            .Where(s => s.ShipmentNumber.StartsWith(prefix))
            .OrderByDescending(s => s.ShipmentNumber)
            .FirstOrDefaultAsync();

        int sequence = 1;
        if (lastShipment != null)
        {
            var lastNumber = lastShipment.ShipmentNumber.Split('-').LastOrDefault();
            if (int.TryParse(lastNumber, out int lastSeq))
            {
                sequence = lastSeq + 1;
            }
        }

        return $"{prefix}-{sequence:D6}";
    }

    /// <summary>
    /// Process courier webhook update
    /// </summary>
    public async Task ProcessCourierWebhookAsync(
        string trackingNumber,
        string status,
        string message,
        string? location,
        DateTime eventTime,
        string courierPayload)
    {
        var shipment = await GetShipmentByTrackingNumberAsync(trackingNumber);
        if (shipment == null)
            return; // Shipment not found, ignore

        // Map courier status to ShipmentStatus
        var mappedStatus = MapCourierStatusToShipmentStatus(status);

        // Update shipment status if different
        if (mappedStatus.HasValue && shipment.Status != mappedStatus.Value)
        {
            await UpdateShipmentStatusAsync(
                shipment.Id,
                mappedStatus.Value,
                message,
                location,
                "System"
            );
        }

        // Always add tracking event
        await AddTrackingEventAsync(
            shipment.Id,
            mappedStatus ?? shipment.Status,
            message,
            location,
            "Webhook",
            courierPayload
        );
    }

    /// <summary>
    /// Map courier-specific status to internal ShipmentStatus
    /// </summary>
    private ShipmentStatus? MapCourierStatusToShipmentStatus(string courierStatus)
    {
        // Normalize status string
        var status = courierStatus.ToLower().Trim();

        // Map common courier statuses
        if (status.Contains("delivered") || status.Contains("completed"))
            return ShipmentStatus.Delivered;
        
        if (status.Contains("out for delivery"))
            return ShipmentStatus.OutForDelivery;
        
        if (status.Contains("transit") || status.Contains("in transit"))
            return ShipmentStatus.InTransit;
        
        if (status.Contains("picked") || status.Contains("pickup"))
            return ShipmentStatus.PickedUp;
        
        if (status.Contains("packed") || status.Contains("ready"))
            return ShipmentStatus.Packed;
        
        if (status.Contains("cancel") || status.Contains("cancelled"))
            return ShipmentStatus.Cancelled;
        
        if (status.Contains("failed") || status.Contains("returned"))
            return ShipmentStatus.Returned;

        return null; // Unknown status, keep existing
    }
}
