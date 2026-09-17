using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Orders;
using Microsoft.Extensions.Logging;

namespace Sparkle.Infrastructure.Services;

/// <summary>
/// Service for sending shipment notifications (Email/SMS)
/// </summary>
public class ShipmentNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ShipmentNotificationService> _logger;

    public ShipmentNotificationService(
        ApplicationDbContext context, 
        INotificationService notificationService,
        ILogger<ShipmentNotificationService> logger)
    {
        _context = context;
        _notificationService = notificationService;
        _logger = logger;
    }

    /// <summary>
    /// Notify customer about shipment status change
    /// </summary>
    public async Task NotifyShipmentStatusChangeAsync(int shipmentId, ShipmentStatus newStatus)
    {
        var shipment = await _context.Shipments
            .Include(s => s.Order)
                .ThenInclude(o => o.User)
            .Include(s => s.Seller)
            .FirstOrDefaultAsync(s => s.Id == shipmentId);

        if (shipment == null) return;

        var customer = shipment.Order.User;
        var (subject, message) = GetNotificationContent(shipment, newStatus);

        // Send Email
        await SendEmailAsync(customer.Email, subject, message);

        // Send SMS (if phone number available) - PhoneNumber is from IdentityUser base class
        // if (!string.IsNullOrEmpty(customer.PhoneNumber))
        // {
        //     await SendSmsAsync(customer.PhoneNumber, message);
        // }

        // Log notification
        await LogNotificationAsync(
            userId: customer.Id,
            type: "ShipmentUpdate",
            title: subject,
            message: message
        );
    }

    /// <summary>
    /// Notify customer when shipment is created
    /// </summary>
    public async Task NotifyShipmentCreatedAsync(int shipmentId)
    {
        var shipment = await _context.Shipments
            .Include(s => s.Order)
                .ThenInclude(o => o.User)
            .Include(s => s.Seller)
            .FirstOrDefaultAsync(s => s.Id == shipmentId);

        if (shipment == null) return;

        var customer = shipment.Order.User;
        var subject = $"Your Order {shipment.Order.OrderNumber} has been Shipped!";
        var message = $@"
            Good news! Your order from {shipment.Seller.ShopName} has been shipped.
            
            Shipment Details:
            - Shipment Number: {shipment.ShipmentNumber}
            - Courier: {shipment.CourierName}
            {(!string.IsNullOrEmpty(shipment.TrackingNumber) ? $"- Tracking Number: {shipment.TrackingNumber}" : "")}
            
            Track your shipment: https://yourdomain.com/tracking?number={shipment.TrackingNumber ?? shipment.ShipmentNumber}
            
            Thank you for shopping with us!
        ";

        await SendEmailAsync(customer.Email, subject, message);
        
        // SMS notification commented out - PhoneNumber from IdentityUser
        // if (!string.IsNullOrEmpty(customer.PhoneNumber))
        // {
        //     var smsMessage = $"Your order {shipment.Order.OrderNumber} from {shipment.Seller.ShopName} has been shipped via {shipment.CourierName}. Track: {shipment.TrackingNumber}";
        //     await SendSmsAsync(customer.PhoneNumber, smsMessage);
        // }

        await LogNotificationAsync(customer.Id, "ShipmentCreated", subject, message);
    }

    /// <summary>
    /// Notify seller about shipment request
    /// </summary>
    public async Task NotifySellerShipmentRequestAsync(int orderId, int sellerId)
    {
        var seller = await _context.Sellers
            .FirstOrDefaultAsync(s => s.Id == sellerId);
        
        if (seller == null) return;
        
        var sellerUser = await _context.Users.FindAsync(seller.UserId);

        var order = await _context.Orders.FindAsync(orderId);

        if (seller == null || order == null || sellerUser == null) return;

        var subject = "New Order Ready to Ship";
        var message = $@"
            You have a new order ready to be shipped.
            
            Order Number: {order.OrderNumber}
            
            Please create a shipment and provide tracking information.
            
            View Order: https://yourdomain.com/seller/orders/{orderId}
        ";

        await SendEmailAsync(sellerUser.Email, subject, message);
        await LogNotificationAsync(seller.UserId, "ShipmentRequest", subject, message);
    }

    /// <summary>
    /// Get notification content based on status
    /// </summary>
    private (string subject, string message) GetNotificationContent(Shipment shipment, ShipmentStatus status)
    {
        var trackingLink = $"https://yourdomain.com/tracking?number={shipment.TrackingNumber ?? shipment.ShipmentNumber}";

        return status switch
        {
            ShipmentStatus.Packed => (
                $"Order {shipment.Order.OrderNumber} - Packed and Ready",
                $"Your order has been packed and is ready for pickup. Courier: {shipment.CourierName}. Track: {trackingLink}"
            ),
            
            ShipmentStatus.PickedUp => (
                $"Order {shipment.Order.OrderNumber} - Picked Up by Courier",
                $"Your order has been picked up by {shipment.CourierName}. Tracking: {shipment.TrackingNumber}. Track: {trackingLink}"
            ),
            
            ShipmentStatus.InTransit => (
                $"Order {shipment.Order.OrderNumber} - In Transit",
                $"Your order is on its way! Current location: {shipment.StatusMessage}. Track: {trackingLink}"
            ),
            
            ShipmentStatus.OutForDelivery => (
                $"Order {shipment.Order.OrderNumber} - Out for Delivery",
                $"Great news! Your order is out for delivery and will arrive soon. Track: {trackingLink}"
            ),
            
            ShipmentStatus.Delivered => (
                $"Order {shipment.Order.OrderNumber} - Delivered!",
                $"Your order has been delivered successfully! Thank you for shopping with us. We hope you enjoy your purchase!"
            ),
            
            ShipmentStatus.Failed => (
                $"Order {shipment.Order.OrderNumber} - Delivery Attempt Failed",
                $"We couldn't deliver your order. Our courier will attempt redelivery. For assistance, contact support."
            ),
            
            ShipmentStatus.Cancelled => (
                $"Order {shipment.Order.OrderNumber} - Shipment Cancelled",
                $"This shipment has been cancelled. If you have questions, please contact support."
            ),
            
            _ => (
                $"Order {shipment.Order.OrderNumber} - Status Update",
                $"Your shipment status has been updated. Track: {trackingLink}"
            )
        };
    }

    /// <summary>
    /// Send email (placeholder - integrate with your email service)
    /// </summary>
    private async Task SendEmailAsync(string? email, string subject, string message)
    {
        if (string.IsNullOrEmpty(email)) return;

        // Note: Integration point for external email service (e.g., SendGrid, AWS SES)
        // For development/demo purposes, we are logging the email content.
        _logger.LogInformation("[EMAIL SERVICE MOCK] To: {Email}, Subject: {Subject}, BodyLength: {Length}", email, subject, message?.Length ?? 0);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Send SMS (placeholder - integrate with your SMS service)
    /// </summary>
    private async Task SendSmsAsync(string phone, string message)
    {
        if (string.IsNullOrEmpty(phone)) return;

        // Note: Integration point for external SMS service (e.g., Twilio, Nexmo)
        // For development/demo purposes, we are logging the SMS content.
        _logger.LogInformation("[SMS SERVICE MOCK] To: {Phone}, Message: {Message}", phone, message);
        await Task.CompletedTask;
    }

    /// <summary>
    /// Log notification to database
    /// </summary>
    /// <summary>
    /// Log notification to database
    /// </summary>
    private async Task LogNotificationAsync(string userId, string type, string title, string message)
    {
        try
        {
            await _notificationService.NotifyUserAsync(userId, title, message, type);
        }
        catch (Exception ex)
        {
            // Log error but don't throw
             _logger.LogError(ex, "Error logging notification");
        }
    }

    /// <summary>
    /// Send bulk notifications for multiple shipments
    /// </summary>
    public async Task SendBulkShipmentNotificationsAsync(List<int> shipmentIds, ShipmentStatus status)
    {
        foreach (var shipmentId in shipmentIds)
        {
            try
            {
                await NotifyShipmentStatusChangeAsync(shipmentId, status);
                await Task.Delay(100); // Rate limiting
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification for shipment {ShipmentId}", shipmentId);
            }
        }
    }

    /// <summary>
    /// Get notification preferences for user
    /// </summary>
    public async Task<NotificationPreferences> GetUserNotificationPreferencesAsync(string userId)
    {
        var settings = await _context.UserNotificationSettings
            .FirstOrDefaultAsync(s => s.UserId == userId);

        if (settings == null)
        {
            return new NotificationPreferences
            {
                EmailEnabled = true,
                SmsEnabled = true,
                OrderUpdates = true,
                ShipmentUpdates = true,
                PromotionalEmails = true
            };
        }

        return new NotificationPreferences
        {
            EmailEnabled = settings.EmailOrderUpdates,
            SmsEnabled = settings.SmsOrderUpdates,
            OrderUpdates = true,
            ShipmentUpdates = true,
            PromotionalEmails = settings.EmailNewsletter
        };
    }
}

/// <summary>
/// Notification preferences model
/// </summary>
public class NotificationPreferences
{
    public bool EmailEnabled { get; set; }
    public bool SmsEnabled { get; set; }
    public bool OrderUpdates { get; set; }
    public bool ShipmentUpdates { get; set; }
    public bool PromotionalEmails { get; set; }
}
