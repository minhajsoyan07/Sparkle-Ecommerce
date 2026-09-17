namespace Sparkle.Api.Models.ViewModels;

public class PaymentStatusViewModel
{
    public string Type { get; set; } = "info"; // success, failed, cancelled
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string? OrderIds { get; set; }
    public decimal Amount { get; set; }
}
