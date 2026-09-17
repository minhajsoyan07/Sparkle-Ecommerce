using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Orders;
using Sparkle.Infrastructure;
using Sparkle.Infrastructure.Services;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
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

    public async Task<IActionResult> Index(string status = "all", string q = "", int page = 1, string sort = "latest")
    {
        var query = _db.Orders
            .Include(o => o.User)
            .Include(o => o.Seller) // Include seller for shop name
            .Include(o => o.OrderItems)
            .AsQueryable();

        // Search by order number, customer name/email, or shop name
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(o => 
                o.OrderNumber.ToLower().Contains(term) ||
                o.Id.ToString().Contains(term) ||
                (o.User != null && ((o.User.FullName != null && o.User.FullName.ToLower().Contains(term)) || 
                                   (o.User.Email != null && o.User.Email.ToLower().Contains(term)))) ||
                (o.Seller != null && o.Seller.ShopName.ToLower().Contains(term))
            );
        }

        // Filter by Status
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (Enum.TryParse<OrderStatus>(status, true, out var orderStatus))
            {
                query = query.Where(o => o.Status == orderStatus);
            }
        }

        // Sorting Logic
        query = sort switch
        {
            "oldest" => query.OrderBy(o => o.OrderDate),
            "amount_desc" => query.OrderByDescending(o => o.TotalAmount),
            "amount_asc" => query.OrderBy(o => o.TotalAmount),
            "latest" or _ => query.OrderByDescending(o => o.OrderDate)
        };

        var totalItems = await query.CountAsync();
        var pageSize = 20;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var orders = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get counts for status tabs
        // Note: These should ideally be cached or optimized if performance is critical
        ViewBag.AllCount = await _db.Orders.CountAsync();
        ViewBag.PendingCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending);
        ViewBag.ProcessingCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Processing || o.Status == OrderStatus.SellerPreparing);
        ViewBag.ShippedCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Shipped || o.Status == OrderStatus.OutForDelivery);
        ViewBag.DeliveredCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Delivered);
        ViewBag.CancelledCount = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Cancelled);

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentSort = sort;
        ViewBag.SearchQuery = q;
        ViewBag.TotalItems = totalItems;
        ViewBag.PageStart = (page - 1) * pageSize + 1;
        ViewBag.PageEnd = Math.Min(page * pageSize, totalItems);

        return View(orders);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.Seller) // Include seller for shop info
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.ProductVariant)
            .Include(o => o.ShippingAddress)
            .Include(o => o.Shipments)
            .Include(o => o.TrackingHistory)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        // Get all available statuses for the dropdown
        ViewBag.AllStatuses = Enum.GetValues<OrderStatus>();

        return View(order);
    }

    /// <summary>
    /// Admin override order status - allows admin to change order status to any valid state
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus newStatus, string? notes)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        var previousStatus = order.Status;

        // Prevent changing delivered orders without explicit confirmation
        if (previousStatus == OrderStatus.Delivered && newStatus != OrderStatus.Delivered)
        {
            TempData["Warning"] = "Note: This order was already marked as delivered. Status has been changed as requested.";
        }

        order.Status = newStatus;

        // Update timestamps based on new status
        switch (newStatus)
        {
            case OrderStatus.Shipped:
            case OrderStatus.OutForDelivery:
                order.ShippedAt ??= DateTime.UtcNow;
                break;
            case OrderStatus.Delivered:
                order.DeliveredAt = DateTime.UtcNow;
                break;
            case OrderStatus.Cancelled:
                order.CancelledAt = DateTime.UtcNow;
                order.CancellationReason = $"[Admin Override] {notes ?? "No reason provided"}";
                break;
        }

        // Add tracking entry
        _db.Set<OrderTracking>().Add(new OrderTracking
        {
            OrderId = order.Id,
            Status = newStatus,
            StatusMessage = $"Status changed from {previousStatus} to {newStatus} by admin{(string.IsNullOrEmpty(notes) ? "" : $": {notes}")}",
            TrackedAt = DateTime.UtcNow,
            Notes = notes
        });

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Order status updated from {previousStatus} to {newStatus}.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Approve a refund request for an order
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveRefund(int id, decimal refundAmount, string? notes)
    {
        var order = await _db.Orders.Include(o => o.User).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        // Create refund record
        var refund = new Refund
        {
            RefundNumber = $"REF-{DateTime.UtcNow:yyyyMMddHHmmss}-{order.Id}",
            OrderId = order.Id,
            UserId = order.UserId,
            RefundAmount = refundAmount,
            RefundReason = "Admin approved refund",
            RefundDescription = notes,
            RefundStatus = "Approved",
            RefundMethod = order.PaymentMethod,
            RequestedAt = DateTime.UtcNow,
            ApprovedAt = DateTime.UtcNow,
            ApprovedBy = User.Identity?.Name ?? "Admin"
        };

        _db.Set<Refund>().Add(refund);

        // Update order status if full refund
        if (refundAmount >= order.TotalAmount)
        {
            order.Status = OrderStatus.Refunded;
            order.PaymentStatus = PaymentStatus.Refunded;
        }
        else
        {
            order.PaymentStatus = PaymentStatus.PartiallyRefunded;
        }

        // Add tracking
        _db.Set<OrderTracking>().Add(new OrderTracking
        {
            OrderId = order.Id,
            Status = order.Status,
            StatusMessage = $"Refund of ৳{refundAmount:N2} approved by admin",
            TrackedAt = DateTime.UtcNow,
            Notes = notes
        });

        await _db.SaveChangesAsync();
        
        // Process wallet refund
        try
        {
            await _walletService.ProcessRefundAsync(order.Id, refundAmount);
            _logger.LogInformation("Wallet refund processed for order {OrderId}: {Amount}", order.Id, refundAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process wallet refund for order {OrderId}", order.Id);
            TempData["Warning"] = "Refund approved but wallet adjustment failed. Please check wallet transactions.";
        }

        TempData["Success"] = $"Refund of ৳{refundAmount:N2} has been approved and wallet adjusted.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Admin can override and cancel orders
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id, string reason)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        // Cannot cancel already delivered or cancelled orders
        if (order.Status == OrderStatus.Delivered)
        {
            TempData["Error"] = "Cannot cancel a delivered order. Consider processing a refund instead.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            TempData["Error"] = "Order is already cancelled.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var previousStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancellationReason = $"[Admin Cancel] {reason ?? "Cancelled by administrator"}";

        // Add to tracking history
        _db.Set<OrderTracking>().Add(new OrderTracking
        {
            OrderId = order.Id,
            Status = OrderStatus.Cancelled,
            StatusMessage = $"Order cancelled by admin (was {previousStatus}): {reason ?? "No reason provided"}",
            TrackedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
        
        // If order was paid and had pending balance, release it back (reverse the hold)
        if (order.PaymentStatus == PaymentStatus.Paid || order.PaymentStatus == PaymentStatus.Pending)
        {
            try
            {
                // Process as refund to reverse any held amounts
                await _walletService.ProcessRefundAsync(order.Id, order.TotalAmount);
                _logger.LogInformation("Released held funds for cancelled order {OrderId}", order.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to release held funds for cancelled order {OrderId}", order.Id);
            }
        }

        TempData["Success"] = "Order has been cancelled successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Mark order as delivered (admin override)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkDelivered(int id, string? notes)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        if (order.Status == OrderStatus.Delivered)
        {
            TempData["Warning"] = "Order is already marked as delivered.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (order.Status == OrderStatus.Cancelled)
        {
            TempData["Error"] = "Cannot mark a cancelled order as delivered.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var previousStatus = order.Status;
        order.Status = OrderStatus.Delivered;
        order.DeliveredAt = DateTime.UtcNow;
        order.PaymentStatus = PaymentStatus.Paid; // Assume paid on delivery

        _db.Set<OrderTracking>().Add(new OrderTracking
        {
            OrderId = order.Id,
            Status = OrderStatus.Delivered,
            StatusMessage = $"Order marked as delivered by admin (was {previousStatus})",
            TrackedAt = DateTime.UtcNow,
            Notes = notes
        });

        await _db.SaveChangesAsync();
        
        // Clear pending balance to available (release funds to seller)
        try
        {
            await _walletService.ClearPendingToAvailableAsync(order.Id);
            _logger.LogInformation("Released funds to seller for delivered order {OrderId}", order.Id);
            TempData["Success"] = "Order marked as delivered and seller funds released.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to release funds for order {OrderId}", order.Id);
            TempData["Warning"] = "Order marked as delivered but fund release failed. Please check wallet transactions.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Add internal note to an order
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNote(int id, string note)
    {
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        // Append note with timestamp
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
        var newNote = $"[{timestamp}] {note}";
        
        order.InternalNotes = string.IsNullOrEmpty(order.InternalNotes) 
            ? newNote 
            : $"{order.InternalNotes}\n{newNote}";

        await _db.SaveChangesAsync();

        TempData["Success"] = "Note added successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
