using Sparkle.Domain.Common;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Sellers;

namespace Sparkle.Domain.Logistics;

// ==================== HUB SYSTEM ====================

public enum HubType
{
    Central = 0,   // Main processing hub
    Regional = 1,  // Regional distribution hub
    District = 2   // District-level hub
}

public enum HubOperationalStatus
{
    Operational = 0,
    Maintenance = 1,
    Closed = 2
}

public class Hub : BaseEntity
{
    public string HubCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public HubType Type { get; set; }
    
    // Location
    public string Address { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Division { get; set; } = string.Empty;
    public string? PostalCode { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    
    // Capacity & Status
    public int Capacity { get; set; } = 1000;
    public int CurrentInventory { get; set; } = 0;
    public HubOperationalStatus OperationalStatus { get; set; } = HubOperationalStatus.Operational;
    
    // Contact
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? ManagerName { get; set; }
    public string? ManagerPhone { get; set; }
    
    // Operating Hours
    public string? OperatingHours { get; set; } // JSON: {"mon": "9:00-18:00", ...}
    
    // Service Area
    public string? ServiceAreas { get; set; } // JSON array of covered areas
    
    // Parent Hub (for hierarchy)
    public int? ParentHubId { get; set; }
    public Hub? ParentHub { get; set; }
    
    public ICollection<Hub> ChildHubs { get; set; } = new List<Hub>();
    public ICollection<HubInventory> Inventory { get; set; } = new List<HubInventory>();
    public ICollection<Rider> AssignedRiders { get; set; } = new List<Rider>();
}

public class HubInventory : BaseEntity
{
    public int HubId { get; set; }
    public Hub Hub { get; set; } = null!;
    
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public string Status { get; set; } = "Received"; // Received, QCPassed, QCFailed, Sorting, ReadyForDispatch, Dispatched
    public string? BarcodeScanned { get; set; }
    
    // Quality Check
    public bool QCPassed { get; set; }
    public string? QCNotes { get; set; }
    public string? QCPerformedBy { get; set; }
    public DateTime? QCPerformedAt { get; set; }
    
    // Timestamps
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SortedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    
    public string? DispatchedTo { get; set; } // Rider ID or next hub
}

// ==================== RIDER SYSTEM ====================

public enum RiderStatus
{
    Active = 0,
    Inactive = 1,
    OnBreak = 2,
    OnDelivery = 3
}

public enum RiderType
{
    Pickup = 0,    // Picks up from sellers
    Delivery = 1,  // Delivers to customers
    Both = 2       // Can do both
}

public class Rider : BaseEntity
{
    public string RiderCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? NidNumber { get; set; }
    
    // Vehicle
    public string? VehicleType { get; set; } // Bicycle, Motorcycle, Van
    public string? VehicleNumber { get; set; }
    
    // Assignment
    public int? AssignedHubId { get; set; }
    public Hub? AssignedHub { get; set; }
    
    public RiderType Type { get; set; } = RiderType.Both;
    public RiderStatus Status { get; set; } = RiderStatus.Active;
    
    // Location (for real-time tracking)
    public double? CurrentLatitude { get; set; }
    public double? CurrentLongitude { get; set; }
    public DateTime? LastLocationUpdate { get; set; }
    
    // Performance
    public int TotalDeliveries { get; set; } = 0;
    public int SuccessfulDeliveries { get; set; } = 0;
    public decimal Rating { get; set; } = 5.0m;
    
    public ICollection<PickupRequest> PickupRequests { get; set; } = new List<PickupRequest>();
    public ICollection<DeliveryAssignment> DeliveryAssignments { get; set; } = new List<DeliveryAssignment>();
}

// ==================== PICKUP SYSTEM ====================

public enum PickupStatus
{
    Scheduled = 0,
    Assigned = 1,
    InProgress = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public class PickupRequest : BaseEntity
{
    public string PickupNumber { get; set; } = string.Empty;
    
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;
    
    public int? AssignedRiderId { get; set; }
    public Rider? AssignedRider { get; set; }
    
    public int? DestinationHubId { get; set; }
    public Hub? DestinationHub { get; set; }
    
    public PickupStatus Status { get; set; } = PickupStatus.Scheduled;
    
    // Scheduling
    public DateTime ScheduledAt { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? PickedUpAt { get; set; }
    public DateTime? DeliveredToHubAt { get; set; }
    
    // Pickup Address (from seller)
    public string PickupAddress { get; set; } = string.Empty;
    public string PickupPhone { get; set; } = string.Empty;
    public double? PickupLatitude { get; set; }
    public double? PickupLongitude { get; set; }
    
    // Failure handling
    public int AttemptCount { get; set; } = 0;
    public string? FailureReason { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    
    public string? Notes { get; set; }
}

// ==================== DELIVERY ASSIGNMENT ====================

public enum DeliveryStatus
{
    Assigned = 0,
    PickedFromHub = 1,
    InTransit = 2,
    AtLocation = 3,
    Delivered = 4,
    Failed = 5,
    Returned = 6
}

public class DeliveryAssignment : BaseEntity
{
    public string DeliveryNumber { get; set; } = string.Empty;
    
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public int RiderId { get; set; }
    public Rider Rider { get; set; } = null!;
    
    public int? SourceHubId { get; set; }
    public Hub? SourceHub { get; set; }
    
    public DeliveryStatus Status { get; set; } = DeliveryStatus.Assigned;
    
    // Timestamps
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PickedFromHubAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    
    // Delivery attempts
    public int AttemptCount { get; set; } = 0;
    public string? FailureReason { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    
    // Proof of delivery
    public string? DeliveryPhoto { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverRelation { get; set; }
    public string? Signature { get; set; } // Base64 or URL
    
    // COD Collection
    public decimal? CodAmount { get; set; }
    public bool CodCollected { get; set; }
    
    public string? Notes { get; set; }
}
