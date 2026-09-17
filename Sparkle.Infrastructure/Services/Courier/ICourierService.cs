namespace Sparkle.Infrastructure.Services.Courier;

/// <summary>
/// Interface for courier API integration
/// </summary>
public interface ICourierService
{
    /// <summary>
    /// Courier name (e.g., "Pathao", "Steadfast", "Redx")
    /// </summary>
    string CourierName { get; }

    /// <summary>
    /// Create a shipment with the courier
    /// </summary>
    Task<CourierShipmentResponse> CreateShipmentAsync(CourierShipmentRequest request);

    /// <summary>
    /// Get tracking information from courier
    /// </summary>
    Task<CourierTrackingResponse> GetTrackingInfoAsync(string trackingNumber);

    /// <summary>
    /// Cancel a shipment
    /// </summary>
    Task<bool> CancelShipmentAsync(string courierShipmentId);

    /// <summary>
    /// Validate webhook signature
    /// </summary>
    bool ValidateWebhookSignature(string payload, string signature);
}

/// <summary>
/// Request model for creating shipment
/// </summary>
public class CourierShipmentRequest
{
    public string RecipientName { get; set; } = string.Empty;
    public string RecipientPhone { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Area { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public decimal WeightKg { get; set; }
    public decimal CashOnDelivery { get; set; }
    public string ItemDescription { get; set; } = string.Empty;
    public string MerchantOrderId { get; set; } = string.Empty;
}

/// <summary>
/// Response from courier shipment creation
/// </summary>
public class CourierShipmentResponse
{
    public bool Success { get; set; }
    public string? CourierShipmentId { get; set; }
    public string? TrackingNumber { get; set; }
    public string? TrackingUrl { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RawResponse { get; set; }
}

/// <summary>
/// Tracking information from courier
/// </summary>
public class CourierTrackingResponse
{
    public bool Success { get; set; }
    public string? TrackingNumber { get; set; }
    public string? Status { get; set; }
    public string? CurrentLocation { get; set; }
    public DateTime? EstimatedDelivery { get; set; }
    public List<CourierTrackingEvent> Events { get; set; } = new();
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Individual tracking event
/// </summary>
public class CourierTrackingEvent
{
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Location { get; set; }
    public DateTime EventTime { get; set; }
}
