using Microsoft.AspNetCore.Mvc;
using Sparkle.Infrastructure.Services;

namespace Sparkle.Api.Controllers;

/// <summary>
/// Public tracking controller - no authentication required
/// </summary>
public class TrackingController : Controller
{
    private readonly ShipmentService _shipmentService;

    public TrackingController(ShipmentService shipmentService)
    {
        _shipmentService = shipmentService;
    }

    // GET: /Tracking
    public IActionResult Index()
    {
        return View();
    }

    // GET: /Tracking/Track?number=SHP-20251122-000001
    [HttpGet]
    public async Task<IActionResult> Track(string number)
    {
        if (string.IsNullOrEmpty(number))
        {
            TempData["Error"] = "Please enter a tracking number";
            return RedirectToAction("Index");
        }

        var shipment = await _shipmentService.GetShipmentByTrackingNumberAsync(number);

        if (shipment == null)
        {
            // Try by shipment number
            shipment = await _shipmentService.GetShipmentByTrackingNumberAsync(number);
        }

        if (shipment == null)
        {
            TempData["Error"] = $"No shipment found with tracking number: {number}";
            return RedirectToAction("Index");
        }

        var timeline = await _shipmentService.GetTrackingTimelineAsync(shipment.Id);
        ViewBag.Timeline = timeline;

        return View(shipment);
    }

    // API endpoint for tracking
    [HttpGet("api/track/{trackingNumber}")]
    public async Task<IActionResult> ApiTrack(string trackingNumber)
    {
        var shipment = await _shipmentService.GetShipmentByTrackingNumberAsync(trackingNumber);

        if (shipment == null)
        {
            return NotFound(new { success = false, message = "Shipment not found" });
        }

        var timeline = await _shipmentService.GetTrackingTimelineAsync(shipment.Id);

        return Json(new
        {
            success = true,
            shipment = new
            {
                shipmentNumber = shipment.ShipmentNumber,
                trackingNumber = shipment.TrackingNumber,
                courierName = shipment.CourierName,
                status = shipment.Status.ToString(),
                statusMessage = shipment.StatusMessage,
                recipientName = shipment.RecipientName,
                shippingCity = shipment.ShippingCity,
                estimatedDelivery = shipment.EstimatedDeliveryDate,
                createdAt = shipment.CreatedAt,
                deliveredAt = shipment.DeliveredAt
            },
            timeline = timeline.Select(t => new
            {
                status = t.Status,
                message = t.Message,
                location = t.Location,
                eventTime = t.EventTime
            })
        });
    }
}
