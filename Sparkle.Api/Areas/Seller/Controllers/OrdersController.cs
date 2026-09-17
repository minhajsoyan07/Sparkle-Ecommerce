using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Orders;
using Sparkle.Infrastructure;
using Sparkle.Infrastructure.Services;
using Sparkle.Api.Areas.Seller.Models;

using Microsoft.Extensions.Logging;

namespace Sparkle.Api.Areas.Seller.Controllers;

[Area("Seller")]
[Authorize(Roles = "Seller")]
[Route("Seller/Orders")]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWalletService _walletService;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(ApplicationDbContext db, IWalletService walletService, ILogger<OrdersController> logger)
    {
        _db = db;
        _walletService = walletService;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException();

    [HttpGet]
    [Route("")]
    [Route("Index")]
    public async Task<IActionResult> Index(string status = "All", int page = 1)
    {
        try 
        {
            var userId = GetUserId();
            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
            if (seller == null) 
            {
                // Redirect to Setup page if seller entity is missing
                return RedirectToAction("Setup", "Dashboard", new { area = "Seller" });
            }

            int pageSize = 10;
            
            // Base query: Find orders that contain items from this seller
            // Use AsSplitQuery to avoid cartesian explosion with multiple Includes
            // Use AsNoTracking for read-only scenarios to improve performance and avoid tracking errors
            var query = _db.Orders
                .AsNoTracking()
                .AsSplitQuery()
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product) 
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductVariant)
                .Where(o => o.OrderItems.Any(oi => oi.SellerId == seller.Id));

            // Filter by status if provided (and not "All")
            if (!string.IsNullOrEmpty(status) && status != "All")
            {
                 if (Enum.TryParse<OrderStatus>(status, out var statusEnum))
                 {
                     query = query.Where(o => o.Status == statusEnum);
                 }
            }

            // Pagination
            int totalItems = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Batched query for user order counts
            var userIds = orders.Select(o => o.UserId).Distinct().ToList();
            var userOrderCounts = new Dictionary<string, int>();
            
            if (userIds.Any())
            {
                // Simple group by to avoid complex translation issues
                var counts = await _db.Orders
                    .AsNoTracking()
                    .Where(o => userIds.Contains(o.UserId))
                    .Select(o => new { o.UserId }) // specific projection
                    .ToListAsync();
                    
                userOrderCounts = counts
                    .GroupBy(x => x.UserId)
                    .ToDictionary(g => g.Key, g => g.Count());
            }

            // Map to ViewModel
            var viewModel = orders.Select(order => 
            {
                var sellerItems = order.OrderItems.Where(oi => oi.SellerId == seller.Id).ToList();
                var initials = order.User?.FullName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(n => n.FirstOrDefault()).Take(2).ToArray();
                
                return new SellerOrderViewModel
                {
                   Order = order,
                   SellerItemCount = sellerItems.Count,
                   SellerTotal = sellerItems.Sum(i => i.TotalPrice),
                   IsReturningCustomer = userOrderCounts.GetValueOrDefault(order.UserId, 0) > 1,
                   MemberSince = order.User?.RegisteredAt.Year.ToString() ?? DateTime.Now.Year.ToString(),
                   CustomerAvatarText = initials != null && initials.Length > 0 ? string.Join("", initials) : "G"
                };
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
            ViewBag.StatusFilter = status;
            ViewBag.SellerId = seller.Id;

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in Seller/Orders/Index");
            // Return an empty view or error view to prevent crash
            return View(new List<SellerOrderViewModel>());
        }
    }

    [HttpGet("Details/{id}")]
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return LocalRedirect($"/auth/register-seller?returnUrl=/Seller/Orders&t={DateTime.Now.Ticks}");

        var order = await _db.Orders
            .AsNoTracking()
            .AsSplitQuery()
            .Include(o => o.User)
            .Include(o => o.ShippingAddress)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product) 
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.ProductVariant) 
            .Include(o => o.Shipments)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        // Verify this order belongs to the seller (has at least one item from them)
        if (!order.OrderItems.Any(oi => oi.SellerId == seller.Id))
        {
            return Unauthorized();
        }

        ViewBag.SellerId = seller.Id;
        return View(order);
    }

    // POST: Update order status (Approve, Confirm, Process, Ship, Deliver)
    [HttpPost("UpdateStatus/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string newStatus)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return Unauthorized();

        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        // Verify order belongs to this seller
        if (!order.OrderItems.Any(oi => oi.SellerId == seller.Id))
        {
            return Unauthorized();
        }

        // Parse and validate new status
        if (!Enum.TryParse<OrderStatus>(newStatus, out var statusEnum))
        {
            TempData["Error"] = "Invalid status";
            return RedirectToAction("Details", new { id });
        }

        // Validate status transitions
        var allowedTransitions = new Dictionary<OrderStatus, OrderStatus[]>
        {
            { OrderStatus.Pending, new[] { OrderStatus.Confirmed, OrderStatus.Cancelled } },
            { OrderStatus.Confirmed, new[] { OrderStatus.Processing, OrderStatus.Cancelled } },
            { OrderStatus.Processing, new[] { OrderStatus.Shipped, OrderStatus.Cancelled } },
            { OrderStatus.Shipped, new[] { OrderStatus.OutForDelivery, OrderStatus.Delivered } },
            { OrderStatus.OutForDelivery, new[] { OrderStatus.Delivered } },
        };

        if (allowedTransitions.TryGetValue(order.Status, out var allowed) && !allowed.Contains(statusEnum))
        {
            TempData["Error"] = $"Cannot transition from {order.Status} to {statusEnum}";
            return RedirectToAction("Details", new { id });
        }

        // Update order status
        order.Status = statusEnum;
        
        // Update timestamps
        if (statusEnum == OrderStatus.Shipped)
        {
            order.ShippedAt = DateTime.UtcNow;
            // Also update any pending shipments to Shipped
            var shipments = await _db.Set<Shipment>().Where(s => s.OrderId == id && s.SellerId == seller.Id && s.Status == ShipmentStatus.Pending).ToListAsync();
            foreach (var ship in shipments) { ship.Status = ShipmentStatus.Shipped; ship.ShippedAt = DateTime.UtcNow; }
        }
        else if (statusEnum == OrderStatus.Delivered)
        {
            order.DeliveredAt = DateTime.UtcNow;
            // Update shipments to Delivered
            var shipments = await _db.Set<Shipment>().Where(s => s.OrderId == id && s.SellerId == seller.Id).ToListAsync();
            foreach (var ship in shipments) { ship.Status = ShipmentStatus.Delivered; ship.DeliveredAt = DateTime.UtcNow; }
            
            // Release funds to seller
            try
            {
                await _walletService.ClearPendingToAvailableAsync(order.Id, seller.Id);
                _logger.LogInformation("Released funds to seller {SellerId} for delivered order {OrderId}", seller.Id, order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release funds for order {OrderId} (Seller: {SellerId})", order.Id, seller.Id);
                // We still proceed as the order status is updated
            }
        }
        else if (statusEnum == OrderStatus.Cancelled)
        {
            order.CancelledAt = DateTime.UtcNow;
            // Update shipments to Cancelled
            var shipments = await _db.Set<Shipment>().Where(s => s.OrderId == id && s.SellerId == seller.Id).ToListAsync();
            foreach (var ship in shipments) { ship.Status = ShipmentStatus.Cancelled; ship.CancelledAt = DateTime.UtcNow; }
        }

        // Add tracking history
        _db.Set<OrderTracking>().Add(new OrderTracking
        {
            OrderId = order.Id,
            Status = statusEnum,
            StatusMessage = $"Order status changed to {statusEnum}",
            TrackedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Order status updated to {statusEnum}";
        return RedirectToAction("Details", new { id });
    }

    // POST: Cancel order
    [HttpPost("Cancel/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id, string reason)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return Unauthorized();

        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        // Verify order belongs to this seller
        if (!order.OrderItems.Any(oi => oi.SellerId == seller.Id))
        {
            return Unauthorized();
        }

        // Can only cancel orders that are not delivered
        if (order.Status == OrderStatus.Delivered)
        {
            TempData["Error"] = "Cannot cancel delivered orders";
            return RedirectToAction("Details", new { id });
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = reason ?? "Cancelled by seller";

        await _db.SaveChangesAsync();

        TempData["Success"] = "Order has been cancelled";
        return RedirectToAction("Details", new { id });
    }

    // POST: Add tracking information
    [HttpPost("AddTracking/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTracking(int id, string courierName, string trackingNumber)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return Unauthorized();

        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return NotFound();

        // Verify order belongs to this seller
        if (!order.OrderItems.Any(oi => oi.SellerId == seller.Id))
        {
            return Unauthorized();
        }

        // Create or update Shipment entity for this seller
        var shipment = await _db.Set<Shipment>()
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.OrderId == id && s.SellerId == seller.Id);

        if (shipment == null)
        {
            shipment = new Shipment
            {
                OrderId = order.Id,
                SellerId = seller.Id,
                ShipmentNumber = $"SHP-{order.OrderNumber}-{seller.Id}",
                CourierName = courierName,
                TrackingNumber = trackingNumber,
                Status = ShipmentStatus.Shipped,
                ShippedAt = DateTime.UtcNow,
                RecipientName = order.ShippingFullName,
                RecipientPhone = order.ShippingPhone,
                ShippingAddress = order.ShippingAddressLine1 + (string.IsNullOrEmpty(order.ShippingAddressLine2) ? "" : " | " + order.ShippingAddressLine2),
                ShippingCity = order.ShippingCity,
                ShippingDistrict = order.ShippingDistrict,
                ShippingPostalCode = order.ShippingPostalCode
            };

            // Add Shipment Items
            var sellerItems = order.OrderItems.Where(oi => oi.SellerId == seller.Id).ToList();
            foreach (var item in sellerItems)
            {
                shipment.Items.Add(new ShipmentItem
                {
                    OrderItemId = item.Id,
                    Quantity = item.Quantity,
                    ProductName = item.ProductName,
                    ProductSKU = item.ProductSKU,
                    VariantName = item.VariantName
                });
            }

            _db.Set<Shipment>().Add(shipment);
            
            // Log tracking event
             shipment.TrackingEvents.Add(new ShipmentTrackingEvent
            {
                Status = "Shipped",
                NormalizedStatus = ShipmentStatus.Shipped,
                Message = $"Package handed over to {courierName}. Tracking ID: {trackingNumber}",
                Location = seller.City ?? "Seller Location",
                EventTime = DateTime.UtcNow
            });
        }
        else
        {
            shipment.CourierName = courierName;
            shipment.TrackingNumber = trackingNumber;
            shipment.Status = ShipmentStatus.Shipped;
            if (!shipment.ShippedAt.HasValue) shipment.ShippedAt = DateTime.UtcNow;
            
             // Log tracking event update
             shipment.TrackingEvents.Add(new ShipmentTrackingEvent
            {
                Status = "Tracking Updated",
                NormalizedStatus = ShipmentStatus.Shipped,
                Message = $"Tracking info updated. Courier: {courierName}, ID: {trackingNumber}",
                EventTime = DateTime.UtcNow
            });
        }

        // Legacy compatibility
        order.CourierName = courierName;
        order.TrackingNumber = trackingNumber;
        
        // If adding tracking, also update status to Shipped if not already
        if (order.Status == OrderStatus.Processing || order.Status == OrderStatus.Confirmed || order.Status == OrderStatus.Pending)
        {
            order.Status = OrderStatus.Shipped;
            order.ShippedAt = DateTime.UtcNow;
            
            // Add global order tracking history
             _db.Set<OrderTracking>().Add(new OrderTracking
            {
                OrderId = order.Id,
                Status = OrderStatus.Shipped,
                StatusMessage = $"Order shipped via {courierName} ({trackingNumber})",
                TrackedAt = DateTime.UtcNow
            });
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = "Tracking information added successfully";
        return RedirectToAction("Details", new { id });
    }
}
