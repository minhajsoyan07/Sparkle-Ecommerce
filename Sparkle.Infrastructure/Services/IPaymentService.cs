using Sparkle.Domain.Orders;
using System.Threading.Tasks;

namespace Sparkle.Infrastructure.Services;

public interface IPaymentService
{
    Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request);
    Task<PaymentValidationResult> ValidatePaymentAsync(string validationId);
}

public sealed class PaymentInitiationRequest
{
    public required string TransactionId { get; init; }
    public required string OrderIds { get; init; }
    public required Order PrimaryOrder { get; init; }
    public required decimal Amount { get; init; }
    public required PaymentMethodType PaymentMethod { get; init; }
    public required string SuccessUrl { get; init; }
    public required string FailUrl { get; init; }
    public required string CancelUrl { get; init; }
    public required string IpnUrl { get; init; }
}

public sealed class PaymentInitiationResult
{
    public bool IsSuccess { get; init; }
    public string? GatewayPageUrl { get; init; }
    public string? SessionKey { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RawResponse { get; init; }
}

public sealed class PaymentValidationResult
{
    public bool IsValid { get; init; }
    public string Status { get; init; } = string.Empty;
    public string TransactionId { get; init; } = string.Empty;
    public string ValidationId { get; init; } = string.Empty;
    public string? BankTransactionId { get; init; }
    public string? CardType { get; init; }
    public string? CardBrand { get; init; }
    public string? Currency { get; init; }
    public decimal Amount { get; init; }
    public int RiskLevel { get; init; }
    public string? RiskTitle { get; init; }
    public string? ErrorMessage { get; init; }
    public string? RawResponse { get; init; }
}
