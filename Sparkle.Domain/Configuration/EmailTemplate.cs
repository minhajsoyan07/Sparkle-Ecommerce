namespace Sparkle.Domain.Configuration;

public class EmailTemplate
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string Subject { get; set; }
    public required string Body { get; set; }
    public EmailTemplateType TemplateType { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public enum EmailTemplateType
{
    OrderConfirmation,
    OrderShipped,
    OrderDelivered,
    PasswordReset,
    WelcomeEmail,
    NewsletterSubscription,
    SellerApproved,
    SellerRejected,
    PayoutProcessed,
    Custom
}
