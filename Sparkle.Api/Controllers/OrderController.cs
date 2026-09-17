using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Orders;
using Sparkle.Infrastructure;
using Sparkle.Api.Services;

namespace Sparkle.Api.Controllers;

[Authorize(Roles = "User")]
[Route("orders")]
public class OrderController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IInvoiceService _invoiceService;

    public OrderController(ApplicationDbContext db, IInvoiceService invoiceService)
    {
        _db = db;
        _invoiceService = invoiceService;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue(ClaimTypes.Name) ??
        throw new InvalidOperationException("User id not found in token");

    private static readonly HashSet<OrderStatus> CancellableStatuses = new()
    {
        OrderStatus.Pending,
        OrderStatus.Confirmed,
        OrderStatus.Processing,
        OrderStatus.SellerPreparing,
        OrderStatus.OnHold
    };

    private static bool CanCancel(Order order) => CancellableStatuses.Contains(order.Status);

    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, int pageSize = 10, string? status = null)
    {
        if (page < 1) page = 1;
        if (pageSize <= 0) pageSize = 10;

        var userId = GetUserId();
        var query = _db.Orders
            .Include(o => o.Seller)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Images)
            .Where(o => o.UserId == userId);

        // Grouped status filter — each tab covers related enum values
        if (!string.IsNullOrEmpty(status))
        {
            var grouped = status.ToLower() switch
            {
                "processing" => new[] { OrderStatus.Processing, OrderStatus.SellerPreparing, OrderStatus.ReadyForHandover, OrderStatus.OnHold },
                "shipped"    => new[] { OrderStatus.Shipped, OrderStatus.PickupScheduled, OrderStatus.PickedUp, OrderStatus.ReceivedAtHub, OrderStatus.QCPassed, OrderStatus.Sorting, OrderStatus.OutForDelivery, OrderStatus.DeliveryAttempted },
                "returned"   => new[] { OrderStatus.ReturnRequested, OrderStatus.Returned, OrderStatus.Refunded, OrderStatus.DeliveryFailed, OrderStatus.ReturnToHub },
                _ when Enum.TryParse<OrderStatus>(status, true, out var single) => new[] { single },
                _ => Array.Empty<OrderStatus>()
            };
            if (grouped.Length > 0)
                query = query.Where(o => grouped.Contains(o.Status));
        }

        var totalCount = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get count per status for filter badges
        var allUserOrders = _db.Orders.Where(o => o.UserId == userId);
        var statusCounts = await allUserOrders
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        ViewBag.StatusFilter = status ?? "";
        ViewBag.StatusCounts = statusCounts.ToDictionary(x => x.Status, x => x.Count);
        ViewBag.TotalAllOrders = await allUserOrders.CountAsync();

        // Determine the "latest group" — orders placed in the same checkout session
        var latestOrder = await allUserOrders.OrderByDescending(o => o.OrderDate).FirstOrDefaultAsync();
        if (latestOrder != null)
        {
            List<int> latestGroupIds;
            if (!string.IsNullOrEmpty(latestOrder.ParentOrderNumber))
            {
                latestGroupIds = await allUserOrders
                    .Where(o => o.ParentOrderNumber == latestOrder.ParentOrderNumber)
                    .Select(o => o.Id)
                    .ToListAsync();
            }
            else
            {
                // Fallback for old orders before ParentOrderNumber was introduced
                var windowStart = latestOrder.OrderDate.AddMinutes(-2);
                latestGroupIds = await allUserOrders
                    .Where(o => o.OrderDate >= windowStart)
                    .Select(o => o.Id)
                    .ToListAsync();
            }

            ViewBag.LatestGroupIds = latestGroupIds;
            ViewBag.LatestOrderId = latestOrder.Id;
            ViewBag.IsLatestGroupCombined = latestGroupIds.Count > 1;
        }

        var vm = new PagedUserOrdersViewModel(orders, page, pageSize, totalCount, status ?? "");
        return View(vm);
    }

    public record PagedUserOrdersViewModel(
        IReadOnlyCollection<Order> Items,
        int Page,
        int PageSize,
        int TotalCount,
        string StatusFilter = "")
    {
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    }

    // Download invoice for a SINGLE order
    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> DownloadSingle(int id)
    {
        var userId = GetUserId();
        var exists = await _db.Orders.AnyAsync(o => o.Id == id && o.UserId == userId);
        if (!exists) return NotFound();

        var pdfBytes = await _invoiceService.GenerateOrderInvoiceAsync(id);
        return File(pdfBytes, "application/pdf", $"Order_Slip_{id}.pdf");
    }

    // Download combined invoice for orders placed in the same checkout session
    [HttpGet("download-combined")]
    public async Task<IActionResult> DownloadCombined(string ids)
    {
        if (string.IsNullOrEmpty(ids)) return BadRequest();
        var userId = GetUserId();

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var v) ? v : 0)
            .Where(v => v > 0)
            .ToList();

        if (!idList.Any()) return BadRequest();

        // Security: verify all orders belong to this user
        var userOrderIds = await _db.Orders
            .Where(o => idList.Contains(o.Id) && o.UserId == userId)
            .Select(o => o.Id)
            .ToListAsync();

        if (!userOrderIds.Any()) return NotFound();

        var pdfBytes = await _invoiceService.GenerateCombinedInvoiceAsync(userOrderIds);
        var label = userOrderIds.Count == 1 ? $"Order_Slip_{userOrderIds[0]}" : $"Combined_Order_Slip_{userOrderIds.Count}_Orders";
        return File(pdfBytes, "application/pdf", $"{label}.pdf");
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetUserId();
        var order = await _db.Orders
            .Include(o => o.Seller)
            .Include(o => o.ShippingAddress)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Images)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Seller)
            .Include(o => o.Shipments)
            .Include(o => o.TrackingHistory)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (order == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(order.ParentOrderNumber))
        {
            var relatedOrders = await _db.Orders
                .Include(o => o.Seller)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Images)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.Product)
                        .ThenInclude(p => p.Seller)
                .Include(o => o.Shipments)
                .Include(o => o.TrackingHistory)
                .Where(o => o.ParentOrderNumber == order.ParentOrderNumber && o.UserId == userId)
                .ToListAsync();
            
            ViewBag.RelatedOrders = relatedOrders;
        }

        return View(order);
    }

    [HttpGet("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = GetUserId();
        var order = await _db.Orders
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (order == null) return NotFound();
        if (!CanCancel(order))
        {
            TempData["Error"] = "This order can no longer be cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        return View(order);
    }

    [HttpPost("{id:int}/cancel")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string? reason, string? returnUrl)
    {
        var userId = GetUserId();
        var order = await _db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

        if (order == null)
            return NotFound();

        var orders = new List<Order> { order };
        if (!string.IsNullOrWhiteSpace(order.ParentOrderNumber))
        {
            orders = await _db.Orders
                .Include(o => o.OrderItems)
                .Where(o => o.UserId == userId && o.ParentOrderNumber == order.ParentOrderNumber)
                .ToListAsync();
        }

        var cancellableOrders = orders.Where(CanCancel).ToList();
        if (!cancellableOrders.Any())
        {
            TempData["Error"] = "This order can no longer be cancelled from the customer panel.";
            return RedirectToLocalOrderPage(returnUrl, id);
        }

        var cancelledAt = DateTime.UtcNow;
        var cancellationReason = string.IsNullOrWhiteSpace(reason)
            ? "Cancelled by customer."
            : reason.Trim();

        var variantRestocks = cancellableOrders
            .SelectMany(o => o.OrderItems)
            .Where(i => i.ProductVariantId.HasValue && !i.IsRefunded)
            .GroupBy(i => i.ProductVariantId!.Value)
            .Select(g => new { VariantId = g.Key, Quantity = g.Sum(i => i.Quantity) })
            .ToList();

        var variantIds = variantRestocks.Select(v => v.VariantId).ToList();
        var variants = await _db.ProductVariants
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync();

        foreach (var restock in variantRestocks)
        {
            var variant = variants.FirstOrDefault(v => v.Id == restock.VariantId);
            if (variant != null)
            {
                variant.Stock += restock.Quantity;
            }
        }

        foreach (var cancellableOrder in cancellableOrders)
        {
            cancellableOrder.Status = OrderStatus.Cancelled;
            cancellableOrder.CancelledAt = cancelledAt;
            cancellableOrder.CancellationReason = cancellationReason;
            cancellableOrder.UpdatedAt = cancelledAt;
            cancellableOrder.UpdatedBy = userId;

            foreach (var item in cancellableOrder.OrderItems)
            {
                item.ItemStatus = OrderStatus.Cancelled;
                item.UpdatedAt = cancelledAt;
                item.UpdatedBy = userId;
            }

            _db.OrderTrackings.Add(new OrderTracking
            {
                OrderId = cancellableOrder.Id,
                Status = OrderStatus.Cancelled,
                StatusMessage = cancellationReason,
                TrackedAt = cancelledAt,
                UpdatedBy = userId
            });
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = cancellableOrders.Count > 1
            ? "Related orders were cancelled successfully."
            : "Order cancelled successfully.";

        return RedirectToLocalOrderPage(returnUrl, id);
    }

    [HttpGet("confirmation")]
    [AllowAnonymous]
    public async Task<IActionResult> Confirmation(string? ids, int? id)
    {
        if (id.HasValue && string.IsNullOrEmpty(ids)) ids = id.Value.ToString();
        if (string.IsNullOrEmpty(ids)) return Redirect("/");

        var idList = ids.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s, out var val) ? val : 0)
            .Where(v => v > 0)
            .ToList();

        if (!idList.Any()) return Redirect("/");

        var orders = await _db.Orders
            .Include(o => o.Seller)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Images)
            .Where(o => idList.Contains(o.Id))
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();

        if (!orders.Any())
        {
            return NotFound();
        }

        return View(orders);
    }

    [HttpGet("track/{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> Track(int id)
    {
        var order = await _db.Orders
            .Include(o => o.Seller)
            .Include(o => o.ShippingAddress)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
                    .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    private IActionResult RedirectToLocalOrderPage(string? returnUrl, int orderId)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Details), new { id = orderId });
    }
}
