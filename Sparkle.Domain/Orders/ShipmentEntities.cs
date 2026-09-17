using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Sellers;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sparkle.Domain.Orders;

/// <summary>
/// Shipment status for tracking lifecycle
/// </summary>
public enum ShipmentStatus
{
    Pending,        // Shipment created but not packed
    Packed,         // Seller has packed the items
    Shipped,        // Handed over to courier (Legacy/Simple flow)
    PickedUp,       // Courier picked up from seller
    InTransit,      // In transit to customer
    OutForDelivery, // Out for delivery
    Delivered,      // Successfully delivered
    Failed,         // Delivery failed
    Returned,       // Returned to seller
    Cancelled       // Shipment cancelled
}

/// <summary>
/// Represents a shipment for an order (supports multi-vendor split shipments)
/// Each seller in a multi-vendor order will have their own shipment
/// </summary>
public class Shipment : BaseEntity
{
    /// <summary>
    /// Unique shipment number for tracking (e.g., SHP-2025-001234)
    /// </summary>
    public string ShipmentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Type of package (Box, Envelope, Poly Bag)
    /// </summary>
    public string? PackageType { get; set; }

    /// <summary>
    /// Number of boxes in this shipment
    /// </summary>
    public int NumberOfBoxes { get; set; } = 1;
    
    /// <summary>
    /// Order this shipment belongs to
    /// </summary>
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    /// <summary>
    /// Seller fulfilling this shipment
    /// </summary>
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    // ==================== COURIER DETAILS ====================
    
    /// <summary>
    /// Name of courier partner (e.g., Pathao, Steadfast, Redx)
    /// </summary>
    public string CourierName { get; set; } = string.Empty;
    
    /// <summary>
    /// Tracking number from courier
    /// </summary>
    public string? TrackingNumber { get; set; }
    
    /// <summary>
    /// Public tracking URL from courier
    /// </summary>
    public string? TrackingUrl { get; set; }
    
    /// <summary>
    /// Courier's internal shipment ID (AWB number)
    /// </summary>
    public string? CourierShipmentId { get; set; }
    
    /// <summary>
    /// Raw JSON payload from courier API response (for debugging/auditing)
    /// </summary>
    public string? CourierPayload { get; set; }
    
    // ==================== STATUS & TRACKING ====================
    
    /// <summary>
    /// Current shipment status
    /// </summary>
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;
    
    /// <summary>
    /// Human-readable status message
    /// </summary>
    public string? StatusMessage { get; set; }
    
    // ==================== DATES & TIMELINE ====================
    
    /// <summary>
    /// When seller packed the items
    /// </summary>
    public DateTime? PackedAt { get; set; }
    
    /// <summary>
    /// When courier picked up from seller
    /// </summary>
    public DateTime? PickedUpAt { get; set; }
    
    /// <summary>
    /// When shipment was handed to courier (legacy compatibility)
    /// </summary>
    public DateTime? ShippedAt { get; set; }
    
    /// <summary>
    /// When shipment was delivered to customer
    /// </summary>
    public DateTime? DeliveredAt { get; set; }
    
    /// <summary>
    /// Estimated delivery date from courier
    /// </summary>
    public DateTime? EstimatedDeliveryDate { get; set; }
    
    /// <summary>
    /// When shipment was cancelled (if applicable)
    /// </summary>
    public DateTime? CancelledAt { get; set; }
    
    /// <summary>
    /// Reason for cancellation
    /// </summary>
    public string? CancellationReason { get; set; }
    
    // ==================== SHIPPING ADDRESS ====================
    
    /// <summary>
    /// Recipient name (cached from order)
    /// </summary>
    public string RecipientName { get; set; } = string.Empty;
    
    /// <summary>
    /// Recipient phone (cached from order)
    /// </summary>
    public string RecipientPhone { get; set; } = string.Empty;
    
    /// <summary>
    /// Full shipping address (cached from order)
    /// </summary>
    public string ShippingAddress { get; set; } = string.Empty;
    
    /// <summary>
    /// City for shipping
    /// </summary>
    public string ShippingCity { get; set; } = string.Empty;
    
    /// <summary>
    /// District for shipping
    /// </summary>
    public string ShippingDistrict { get; set; } = string.Empty;
    
    /// <summary>
    /// Postal code
    /// </summary>
    public string ShippingPostalCode { get; set; } = string.Empty;
    
    // ==================== DIMENSIONS & WEIGHT ====================
    
    /// <summary>
    /// Package weight in kg (for courier API)
    /// </summary>
    public decimal? WeightKg { get; set; }
    
    /// <summary>
    /// Package length in cm
    /// </summary>
    public decimal? LengthCm { get; set; }
    
    /// <summary>
    /// Package width in cm
    /// </summary>
    public decimal? WidthCm { get; set; }
    
    /// <summary>
    /// Package height in cm
    /// </summary>
    public decimal? HeightCm { get; set; }
    
    // ==================== COST ====================
    
    /// <summary>
    /// Shipping cost charged to customer for this shipment
    /// </summary>
    public decimal ShippingCost { get; set; }
    
    /// <summary>
    /// Actual cost charged by courier to seller
    /// </summary>
    public decimal? CourierCharge { get; set; }
    
    // ==================== NAVIGATION PROPERTIES ====================
    
    /// <summary>
    /// Items included in this shipment
    /// </summary>
    public ICollection<ShipmentItem> Items { get; set; } = new List<ShipmentItem>();
    
    /// <summary>
    /// Tracking events from courier webhooks
    /// </summary>
    public ICollection<ShipmentTrackingEvent> TrackingEvents { get; set; } = new List<ShipmentTrackingEvent>();
    
    // ==================== NOTES ====================
    
    /// <summary>
    /// Internal notes for seller/admin
    /// </summary>
    public string? InternalNotes { get; set; }
    
    /// <summary>
    /// Notes visible to customer
    /// </summary>
    public string? CustomerNotes { get; set; }
}

/// <summary>
/// Represents items within a shipment
/// Links order items to their shipment for split shipment scenarios
/// </summary>
public class ShipmentItem : BaseEntity
{
    /// <summary>
    /// Parent shipment
    /// </summary>
    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    
    /// <summary>
    /// Order item being shipped
    /// </summary>
    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;
    
    /// <summary>
    /// Quantity being shipped (supports partial shipments)
    /// </summary>
    public int Quantity { get; set; }
    
    /// <summary>
    /// SKU of the product (cached for convenience)
    /// </summary>
    public string? ProductSKU { get; set; }
    
    /// <summary>
    /// Product name (cached for convenience)
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// Variant name if applicable (cached)
    /// </summary>
    public string? VariantName { get; set; }
}

/// <summary>
/// Represents tracking events from courier API webhooks
/// Stores the complete timeline of shipment movement
/// </summary>
public class ShipmentTrackingEvent : BaseEntity
{
    /// <summary>
    /// Parent shipment
    /// </summary>
    public int ShipmentId { get; set; }
    public Shipment Shipment { get; set; } = null!;
    
    /// <summary>
    /// Status from courier (e.g., "In Transit", "Out for Delivery")
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Normalized shipment status (mapped to ShipmentStatus enum)
    /// </summary>
    public ShipmentStatus? NormalizedStatus { get; set; }
    
    /// <summary>
    /// Message/description from courier
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Location where event occurred (e.g., "Dhaka Hub", "Chittagong Delivery Center")
    /// </summary>
    public string? Location { get; set; }
    
    /// <summary>
    /// Timestamp of the event (from courier)
    /// </summary>
    public DateTime EventTime { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Raw JSON data from courier webhook (for debugging/auditing)
    /// </summary>
    public string? CourierEventData { get; set; }
    
    /// <summary>
    /// Whether customer has been notified about this event
    /// </summary>
    public bool CustomerNotified { get; set; }
    
    /// <summary>
    /// Source of the event (Webhook, Manual, System)
    /// </summary>
    public string EventSource { get; set; } = "Webhook";
    
    /// <summary>
    /// User who created this event manually (if not from webhook)
    /// </summary>
    public string? CreatedByUserId { get; set; }
}
