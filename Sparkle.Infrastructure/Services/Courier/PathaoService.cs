using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Sparkle.Infrastructure.Services.Courier;

/// <summary>
/// Pathao Courier API Integration
/// </summary>
public class PathaoService : ICourierService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly string _apiKey;
    private readonly string _secretKey;
    private readonly string _baseUrl;

    public string CourierName => "Pathao";

    public PathaoService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        
        // Load from configuration
        _apiKey = configuration["Courier:Pathao:ApiKey"] ?? "your-pathao-api-key";
        _secretKey = configuration["Courier:Pathao:SecretKey"] ?? "your-pathao-secret-key";
        _baseUrl = configuration["Courier:Pathao:BaseUrl"] ?? "https://api.pathao.com/api/v1";
        
        _httpClient.BaseAddress = new Uri(_baseUrl);
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public async Task<CourierShipmentResponse> CreateShipmentAsync(CourierShipmentRequest request)
    {
        try
        {
            var pathaoRequest = new
            {
                recipient_name = request.RecipientName,
                recipient_phone = request.RecipientPhone,
                recipient_address = request.RecipientAddress,
                recipient_city = request.City,
                recipient_area = request.Area,
                item_type = 1, // General goods
                item_weight = request.WeightKg,
                item_description = request.ItemDescription,
                amount_to_collect = request.CashOnDelivery,
                merchant_order_id = request.MerchantOrderId
            };

            var response = await _httpClient.PostAsJsonAsync("/orders/store", pathaoRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<PathaoOrderResponse>(responseContent);
                
                return new CourierShipmentResponse
                {
                    Success = true,
                    CourierShipmentId = result?.data?.consignment_id,
                    TrackingNumber = result?.data?.consignment_id,
                    TrackingUrl = $"https://pathao.com/track/{result?.data?.consignment_id}",
                    RawResponse = responseContent
                };
            }

            return new CourierShipmentResponse
            {
                Success = false,
                ErrorMessage = $"Pathao API error: {responseContent}",
                RawResponse = responseContent
            };
        }
        catch (Exception ex)
        {
            return new CourierShipmentResponse
            {
                Success = false,
                ErrorMessage = $"Exception: {ex.Message}"
            };
        }
    }

    public async Task<CourierTrackingResponse> GetTrackingInfoAsync(string trackingNumber)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/orders/track/{trackingNumber}");
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var result = JsonSerializer.Deserialize<PathaoTrackingResponse>(responseContent);
                
                return new CourierTrackingResponse
                {
                    Success = true,
                    TrackingNumber = trackingNumber,
                    Status = result?.data?.order_status,
                    CurrentLocation = result?.data?.current_location,
                    Events = result?.data?.tracking_history?.Select(h => new CourierTrackingEvent
                    {
                        Status = h.status ?? "",
                        Message = h.message ?? "",
                        Location = h.location ?? "",
                        EventTime = h.timestamp
                    }).ToList() ?? new List<CourierTrackingEvent>()
                };
            }

            return new CourierTrackingResponse
            {
                Success = false,
                ErrorMessage = "Failed to get tracking info"
            };
        }
        catch (Exception ex)
        {
            return new CourierTrackingResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<bool> CancelShipmentAsync(string courierShipmentId)
    {
        try
        {
            var response = await _httpClient.PostAsync($"/orders/cancel/{courierShipmentId}", null);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public bool ValidateWebhookSignature(string payload, string signature)
    {
        try
        {
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_secretKey));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
            var expectedSignature = Convert.ToBase64String(hash);
            return signature == expectedSignature;
        }
        catch
        {
            return false;
        }
    }

    // Internal models for Pathao API
    private class PathaoOrderResponse
    {
        public PathaoOrderData? data { get; set; }
    }

    private class PathaoOrderData
    {
        public string? consignment_id { get; set; }
    }

    private class PathaoTrackingResponse
    {
        public PathaoTrackingData? data { get; set; }
    }

    private class PathaoTrackingData
    {
        public string? order_status { get; set; }
        public string? current_location { get; set; }
        public List<PathaoTrackingHistory>? tracking_history { get; set; }
    }

    private class PathaoTrackingHistory
    {
        public string? status { get; set; }
        public string? message { get; set; }
        public string? location { get; set; }
        public DateTime timestamp { get; set; }
    }
}
