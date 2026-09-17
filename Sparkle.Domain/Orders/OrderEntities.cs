using Sparkle.Domain.Common;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Sellers;

namespace Sparkle.Domain.Orders;

public enum OrderStatus
{
    // Initial
    Pending = 0,
    Confirmed = 1,
    
    // Seller Phase
    SellerPreparing = 10,
    ReadyForHandover = 11,
    
    // Pickup Phase
    PickupScheduled = 20,
    PickedUp = 21,
    PickupFailed = 22,
    
    // Hub Phase
    ReceivedAtHub = 30,
    QCPassed = 31,
    QCFailed = 32,
    Sorting = 33,
    
    // Delivery Phase
    OutForDelivery = 40,
    DeliveryAttempted = 41,
    Delivered = 50,
    
    // Exceptions
    DeliveryFailed = 60,
    ReturnToHub = 61,
    ReturnRequested = 70,
    Returned = 71,
    Refunded = 80,
    Cancelled = 90,
    
    // Legacy mappings (backward compatibility) - use unique values
    Processing = 12,  // Maps to SellerPreparing
    OnHold = 13,
    Shipped = 42      // Legacy - similar to OutForDelivery but distinct value
}

public enum PaymentStatus
{
    Pending,
    Paid,
    Failed,
    Refunded,
    PartiallyRefunded
}

public enum PaymentMethodType
{
    CashOnDelivery,
    BkashPersonal,
    BkashMerchant,
    Nagad,
    Rocket,
    CreditCard,
    DebitCard,
    BankTransfer,
    SparkleWallet,
    Instalment
}

// Keep legacy classes for backward compatibility
public class Address
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string Phone { get; set; } = default!;
    public string Line1 { get; set; } = default!;
    public string? Line2 { get; set; }
    public string City { get; set; } = default!;
    public string State { get; set; } = default!;
    public string? Area { get; set; }
    public string PostalCode { get; set; } = default!;
    public string Country { get; set; } = default!;
    public bool IsDefault { get; set; }
}

public class Cart
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();

    // Added for Coupon feature
    public string? CouponCode { get; set; }
    public decimal DiscountAmount { get; set; }
}

public class Wishlist
{
    public int Id { get; set; }
    [global::System.ComponentModel.DataAnnotations.MaxLength(450)]
    public string UserId { get; set; } = default!;
    public ICollection<WishlistItem> Items { get; set; } = new List<WishlistItem>();
    
    // Sharing features
    public string? ShareToken { get; set; } // Unique token for sharing
    public bool IsPublic { get; set; } = false;
    public DateTime? SharedAt { get; set; }
    public string? Name { get; set; } // Optional name for the wishlist
}

public class WishlistItem
{
    public int Id { get; set; }
    public int WishlistId { get; set; }
    public Wishlist Wishlist { get; set; } = default!;
    public int ProductId { get; set; }
    public Product Product { get; set; } = default!;
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public Cart Cart { get; set; } = default!;
    public int ProductVariantId { get; set; }
    public ProductVariant ProductVariant { get; set; } = default!;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public enum DeliveryMode
{
    PlatformPickup = 0,   // Platform picks from seller
    SellerDrop = 1,       // Seller drops to hub
    CourierAssisted = 2   // Third-party courier
}

public class Order : BaseEntity
{
    public string OrderNumber { get; set; } = string.Empty;
    public string? ParentOrderNumber { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    
    // Order Details
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;
    public PaymentMethodType PaymentMethod { get; set; }
    
    // Delivery Mode (Phase 2)
    public DeliveryMode DeliveryMode { get; set; } = DeliveryMode.PlatformPickup;
    public int? AssignedHubId { get; set; }
    public int? PickupRiderId { get; set; }
    public int? DeliveryRiderId { get; set; }
    public int DeliveryAttempts { get; set; } = 0;
    
    // Amounts
    public decimal SubTotal { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal CouponDiscount { get; set; }
    public decimal VoucherDiscount { get; set; }
    public decimal TotalAmount { get; set; }
    
    // Coupon
    public string? CouponCode { get; set; }
    
    // Payment Details
    public string? PaymentTransactionId { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime? PaymentInitiatedAt { get; set; }
    
    // Shipping Address
    public string ShippingFullName { get; set; } = string.Empty;
    public string ShippingPhone { get; set; } = string.Empty;
    public string ShippingAddressLine1 { get; set; } = string.Empty;
    public string ShippingAddressLine2 { get; set; } = string.Empty;
    public string ShippingArea { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingDistrict { get; set; } = string.Empty;
    public string ShippingDivision { get; set; } = string.Empty;
    public string ShippingPostalCode { get; set; } = string.Empty;
    public string ShippingCountry { get; set; } = "Bangladesh";
    
    // Billing Address (if different)
    public bool BillingAddressSame { get; set; } = true;
    public string? BillingFullName { get; set; }
    public string? BillingPhone { get; set; }
    public string? BillingAddressLine1 { get; set; }
    public string? BillingCity { get; set; }
    public string? BillingDistrict { get; set; }
    public string? BillingPostalCode { get; set; }
    
    // Tracking
    public string? CourierName { get; set; }
    public string? TrackingNumber { get; set; }
    public DateTime? ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancellationReason { get; set; }
    
    // Notes
    public string? CustomerNotes { get; set; }
    public string? InternalNotes { get; set; }
    
    // Timestamps
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;
    public DateTime? EstimatedDeliveryDate { get; set; }
    
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public ICollection<Shipment> Shipments { get; set; } = new List<Shipment>();
    public ICollection<OrderTracking> TrackingHistory { get; set; } = new List<OrderTracking>();
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    // Backward compatibility properties for old controllers/views
    public ICollection<OrderItem> Items => OrderItems;
    public decimal Subtotal { get => SubTotal; set => SubTotal = value; }
    public decimal DiscountTotal { get => DiscountAmount; set => DiscountAmount = value; }
    public decimal ShippingFee { get => ShippingCost; set => ShippingCost = value; }
    public decimal Total { get => TotalAmount; set => TotalAmount = value; }
    public new DateTime CreatedAt { get => OrderDate; set => OrderDate = value; }
    public int? ShippingAddressId { get; set; } // Legacy property, now optional
    public Address? ShippingAddress { get; set; } // Legacy navigation
}

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    public int? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }
    
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    
    // Item Details
    public string ProductName { get; set; } = string.Empty;
    public string? ProductSKU { get; set; }
    public string? VariantName { get; set; }
    public string? ProductImage { get; set; }
    
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalPrice { get; set; }
    
    // Seller Commission
    public decimal PlatformCommissionRate { get; set; } // Percentage
    public decimal PlatformCommissionAmount { get; set; }
    public decimal SellerEarning { get; set; }
    
    // Status
    public OrderStatus ItemStatus { get; set; } = OrderStatus.Pending;
    public bool IsReviewed { get; set; }
    public bool IsRefunded { get; set; }

    // Backward compatibility properties
    public string ProductTitle { get => ProductName; set => ProductName = value; }
    public string? VariantDescription { get => VariantName; set => VariantName = value; }
    public decimal LineTotal { get => TotalPrice; set => TotalPrice = value; }
}

public class OrderTracking : BaseEntity
{
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public OrderStatus Status { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public string? Location { get; set; }
    // UpdatedBy inherited from BaseEntity
    public DateTime TrackedAt { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
}

public class PaymentMethod : BaseEntity
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public PaymentMethodType MethodType { get; set; }
    public string MethodName { get; set; } = string.Empty;
    
    // For Mobile Banking (Bkash, Nagad, Rocket)
    public string? MobileNumber { get; set; }
    public string? AccountName { get; set; }
    
    // For Cards
    public string? CardHolderName { get; set; }
    public string? CardNumberLast4 { get; set; }
    public string? CardBrand { get; set; } // Visa, MasterCard, etc.
    public int? ExpiryMonth { get; set; }
    public int? ExpiryYear { get; set; }
    
    // For Bank Transfer
    public string? BankName { get; set; }
    public string? AccountNumber { get; set; }
    public string? RoutingNumber { get; set; }
    
    public bool IsDefault { get; set; }
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; } = true;
}

public class Transaction : BaseEntity
{
    public string TransactionNumber { get; set; } = string.Empty;
    
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    
    // Transaction Details
    public string TransactionType { get; set; } = string.Empty; // Payment, Refund, Commission, Payout
    public PaymentMethodType PaymentMethod { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "BDT";
    
    // Status
    public string Status { get; set; } = "Pending"; // Pending, Success, Failed, Cancelled
    public string? FailureReason { get; set; }
    
    // Payment Gateway Info
    public string? GatewayTransactionId { get; set; }
    public string? GatewayName { get; set; }
    public string? GatewayResponse { get; set; }
    
    // Mobile Banking Details
    public string? SenderNumber { get; set; }
    public string? ReceiverNumber { get; set; }
    public string? TrxId { get; set; }
    
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    
    public string? Notes { get; set; }
}

public class Refund : BaseEntity
{
    public string RefundNumber { get; set; } = string.Empty;
    
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public int? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    // Refund Details
    public decimal RefundAmount { get; set; }
    public string RefundReason { get; set; } = string.Empty;
    public string? RefundDescription { get; set; }
    public string RefundStatus { get; set; } = "Requested"; // Requested, Approved, Processing, Completed, Rejected
    
    // Refund Method
    public PaymentMethodType RefundMethod { get; set; }
    public string? RefundAccountNumber { get; set; }
    public string? RefundAccountName { get; set; }
    
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public string? RejectionReason { get; set; }
    
    public string? TransactionId { get; set; }
}

public class ReturnRequest : BaseEntity
{
    public string ReturnNumber { get; set; } = string.Empty;
    
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public int OrderItemId { get; set; }
    public OrderItem OrderItem { get; set; } = null!;
    
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    
    public int? SellerId { get; set; }
    public Seller? Seller { get; set; }
    
    // Return Details
    public string ReturnReason { get; set; } = string.Empty;
    public string? ReturnDescription { get; set; }
    public int Quantity { get; set; }
    public decimal RefundAmount { get; set; }
    
    // Status
    public string Status { get; set; } = "Requested"; // Requested, Approved, PickupScheduled, Picked, InTransit, Received, Inspected, Completed, Rejected
    
    // Images/Videos
    public string? Images { get; set; } // JSON array
    public string? Videos { get; set; } // JSON array
    
    // Pickup Details
    public string? PickupAddress { get; set; }
    public string? PickupPhone { get; set; }
    public DateTime? PickupScheduledDate { get; set; }
    public DateTime? PickupCompletedDate { get; set; }
    public string? CourierName { get; set; }
    public string? TrackingNumber { get; set; }
    
    // Resolution
    public string? ResolutionType { get; set; } // Refund, Exchange, StoreCredit
    public string? InspectionNotes { get; set; }
    public DateTime? InspectedAt { get; set; }
    public string? InspectedBy { get; set; }
    
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public string? RejectionReason { get; set; }
}
