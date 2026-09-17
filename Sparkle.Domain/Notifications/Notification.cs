namespace Sparkle.Domain.Notifications;

public class Notification
{
    public int Id { get; set; }
    [global::System.ComponentModel.DataAnnotations.MaxLength(450)]
    public string UserId { get; set; } = null!;
    public required string Title { get; set; }
    public required string Message { get; set; }
    public NotificationType Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public string? ActionUrl { get; set; }
    public string? RelatedEntityId { get; set; }
}

public enum NotificationType
{
    OrderPlaced,
    OrderStatusUpdated,
    PaymentReceived,
    VendorApproved,
    VendorRejected,
    ProductApproved,
    ProductRejected,
    TicketReply,
    PayoutProcessed,
    General
}

public class SmsLog
{
    public int Id { get; set; }
    public required string PhoneNumber { get; set; }
    public required string Message { get; set; }
    public required string Provider { get; set; } // Robi, GP, Airtel, Banglalink
    public bool IsSent { get; set; }
    public DateTime SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}

public class EmailLog
{
    public int Id { get; set; }
    public required string ToEmail { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public bool IsSent { get; set; }
    public DateTime SentAt { get; set; }
    public string? ErrorMessage { get; set; }
}
