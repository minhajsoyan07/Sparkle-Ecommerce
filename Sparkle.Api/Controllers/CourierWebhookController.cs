using Microsoft.AspNetCore.Mvc;
using Sparkle.Infrastructure.Services;
using Sparkle.Infrastructure.Services.Courier;
using System.Text.Json;

namespace Sparkle.Api.Controllers;

/// <summary>
/// Webhook endpoint for courier API callbacks
/// </summary>
[ApiController]
[Route("api/webhooks/courier")]
public class CourierWebhookController : ControllerBase
{
    private readonly ShipmentService _shipmentService;
    private readonly ShipmentNotificationService _notificationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CourierWebhookController> _logger;

    public CourierWebhookController(
        ShipmentService shipmentService,
        ShipmentNotificationService notificationService,
        IServiceProvider serviceProvider,
        ILogger<CourierWebhookController> logger)
    {
        _shipmentService = shipmentService;
        _notificationService = notificationService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    /// Pathao webhook endpoint
    /// </summary>
    [HttpPost("pathao")]
    public async Task<IActionResult> PathaoWebhook([FromBody] JsonElement payload)
    {
        try
        {
            _logger.LogInformation("Received Pathao webhook: {Payload}", payload.GetRawText());

            // Validate signature
            var signature = Request.Headers["X-Pathao-Signature"].ToString();
            var pathaoService = _serviceProvider.GetService<PathaoService>();
            
            if (pathaoService != null && !pathaoService.ValidateWebhookSignature(payload.GetRawText(), signature))
            {
                _logger.LogWarning("Invalid Pathao webhook signature");
                return Unauthorized(new { success = false, message = "Invalid signature" });
            }

            // Parse webhook data
            var trackingNumber = payload.GetProperty("consignment_id").GetString();
            var status = payload.GetProperty("order_status").GetString();
            var message = payload.GetProperty("status_message").GetString();
            var location = payload.GetProperty("current_location").GetString();
            var timestamp = payload.GetProperty("timestamp").GetDateTime();

            if (string.IsNullOrEmpty(trackingNumber))
            {
                return BadRequest(new { success = false, message = "Missing tracking number" });
            }

            // Process update
            await _shipmentService.ProcessCourierWebhookAsync(
                trackingNumber: trackingNumber,
                status: status ?? "Unknown",
                message: message ?? "Status updated",
                location: location,
                eventTime: timestamp,
                courierPayload: payload.GetRawText()
            );

            // Get shipment to send notification
            var shipment = await _shipmentService.GetShipmentByTrackingNumberAsync(trackingNumber);
            if (shipment != null)
            {
                await _notificationService.NotifyShipmentStatusChangeAsync(shipment.Id, shipment.Status);
            }

            _logger.LogInformation("Pathao webhook processed successfully for tracking: {TrackingNumber}", trackingNumber);
            return Ok(new { success = true, message = "Webhook processed" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Pathao webhook");
            return StatusCode(500, new { success = false, message = "Internal server error" });
        }
    }

    /// <summary>
    /// Steadfast webhook endpoint
    /// </summary>
    [HttpPost("steadfast")]
    public async Task<IActionResult> SteadfastWebhook([FromBody] JsonElement payload)
    {
        try
        {
            _logger.LogInformation("Received Steadfast webhook: {Payload}", payload.GetRawText());

            // Parse Steadfast webhook format
            var trackingNumber = payload.GetProperty("tracking_code").GetString();
            var status = payload.GetProperty("delivery_status").GetString();
            var message = payload.GetProperty("note").GetString();
            var timestamp = DateTime.UtcNow;

            if (string.IsNullOrEmpty(trackingNumber))
            {
                return BadRequest(new { success = false, message = "Missing tracking number" });
            }

            await _shipmentService.ProcessCourierWebhookAsync(
                trackingNumber: trackingNumber,
                status: status ?? "Unknown",
                message: message ?? "Status updated",
                location: null,
                eventTime: timestamp,
                courierPayload: payload.GetRawText()
            );

            var shipment = await _shipmentService.GetShipmentByTrackingNumberAsync(trackingNumber);
            if (shipment != null)
            {
                await _notificationService.NotifyShipmentStatusChangeAsync(shipment.Id, shipment.Status);
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Steadfast webhook");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Redx webhook endpoint
    /// </summary>
    [HttpPost("redx")]
    public async Task<IActionResult> RedxWebhook([FromBody] JsonElement payload)
    {
        try
        {
            _logger.LogInformation("Received Redx webhook: {Payload}", payload.GetRawText());

            var trackingNumber = payload.GetProperty("tracking_id").GetString();
            var status = payload.GetProperty("status").GetString();
            var remarks = payload.GetProperty("remarks").GetString();
            var timestamp = DateTime.UtcNow;

            if (string.IsNullOrEmpty(trackingNumber))
            {
                return BadRequest(new { success = false, message = "Missing tracking number" });
            }

            await _shipmentService.ProcessCourierWebhookAsync(
                trackingNumber: trackingNumber,
                status: status ?? "Unknown",
                message: remarks ?? "Status updated",
                location: null,
                eventTime: timestamp,
                courierPayload: payload.GetRawText()
            );

            var shipment = await _shipmentService.GetShipmentByTrackingNumberAsync(trackingNumber);
            if (shipment != null)
            {
                await _notificationService.NotifyShipmentStatusChangeAsync(shipment.Id, shipment.Status);
            }

            return Ok(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Redx webhook");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }

    /// <summary>
    /// Generic webhook endpoint for testing
    /// </summary>
    [HttpPost("test")]
    public async Task<IActionResult> TestWebhook([FromBody] JsonElement payload)
    {
        try
        {
            _logger.LogInformation("Received test webhook: {Payload}", payload.GetRawText());

            // Simple format for testing
            var trackingNumber = payload.GetProperty("trackingNumber").GetString();
            var status = payload.GetProperty("status").GetString();
            var message = payload.GetProperty("message").GetString();
            var location = payload.TryGetProperty("location", out var loc) ? loc.GetString() : null;

            if (string.IsNullOrEmpty(trackingNumber))
            {
                return BadRequest(new { success = false, message = "Missing trackingNumber" });
            }

            await _shipmentService.ProcessCourierWebhookAsync(
                trackingNumber: trackingNumber,
                status: status ?? "InTransit",
                message: message ?? "Test update",
                location: location,
                eventTime: DateTime.UtcNow,
                courierPayload: payload.GetRawText()
            );

            return Ok(new { 
                success = true, 
                message = "Test webhook processed",
                trackingNumber = trackingNumber
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing test webhook");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }
}
