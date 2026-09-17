using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Logistics;
using Sparkle.Infrastructure;
using Sparkle.Infrastructure.Services;

namespace Sparkle.Api.Areas.Seller.Controllers;

[Area("Seller")]
[Authorize(Roles = "Seller")]
public class ShipmentsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ShipmentService _shipmentService;

    public ShipmentsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ShipmentService shipmentService)
    {
        _context = context;
        _userManager = userManager;
        _shipmentService = shipmentService;
    }

    // GET: /Seller/Shipments
    public async Task<IActionResult> Index(
        string? status = null, 
        string? searchQuery = null, 
        DateTime? startDate = null, 
        DateTime? endDate = null,
        int page = 1)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");
        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);
        
        if (seller == null)
            return RedirectToAction("Register", "Seller");

        var query = _context.Shipments
            .Include(s => s.Order)
            .Include(s => s.Items)
            .Where(s => s.SellerId == seller.Id);

        // 1. Calculate Counts for Tabs (before filtering by status)
        // We do this by grouping to get all counts in one DB trip if possible, or separate count queries
        var statusCounts = await _context.Shipments
            .Where(s => s.SellerId == seller.Id)
            .GroupBy(s => s.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count);

        ViewBag.UnshippedCount = statusCounts.GetValueOrDefault(ShipmentStatus.Pending);
        ViewBag.PackedCount = statusCounts.GetValueOrDefault(ShipmentStatus.Packed);
        ViewBag.ShippedCount = statusCounts.GetValueOrDefault(ShipmentStatus.Shipped);
        ViewBag.DeliveredCount = statusCounts.GetValueOrDefault(ShipmentStatus.Delivered);
        ViewBag.CancelledCount = statusCounts.GetValueOrDefault(ShipmentStatus.Cancelled);

        // 2. Apply Search Filters
        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            searchQuery = searchQuery.ToLower().Trim();
            query = query.Where(s => 
                s.ShipmentNumber.ToLower().Contains(searchQuery) ||
                s.Order.Id.ToString().Contains(searchQuery) ||
                s.RecipientName.ToLower().Contains(searchQuery) ||
                s.Items.Any(i => (i.ProductSKU ?? "").ToLower().Contains(searchQuery) || (i.ProductName ?? "").ToLower().Contains(searchQuery))
            );
        }

        if (startDate.HasValue)
        {
            query = query.Where(s => s.CreatedAt >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            // Add 1 day to include the end date fully
            query = query.Where(s => s.CreatedAt < endDate.Value.AddDays(1));
        }

        // 3. Apply Status Filter (Default to Unshipped if not specified)
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<ShipmentStatus>(status, out var shipmentStatus))
        {
            query = query.Where(s => s.Status == shipmentStatus);
        }
        else if (string.IsNullOrEmpty(status)) // Default tab
        {
            query = query.Where(s => s.Status == ShipmentStatus.Pending);
            status = "Pending";
        }

        // 4. Pagination
        var pageSize = 20;
        var totalItems = await query.CountAsync();
        var shipments = await query
            .OrderByDescending(s => s.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        ViewBag.StatusFilter = status;
        ViewBag.SearchQuery = searchQuery;
        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");

        return View(shipments);
    }

    // GET: /Seller/Shipments/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");
        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);

        if (seller == null)
            return RedirectToAction("Register", "Seller");

        var shipment = await _context.Shipments
            .Include(s => s.Order)
            .Include(s => s.Items)
                .ThenInclude(si => si.OrderItem)
                    .ThenInclude(oi => oi.Product)
            .Include(s => s.TrackingEvents.OrderByDescending(te => te.EventTime))
            .FirstOrDefaultAsync(s => s.Id == id && s.SellerId == seller.Id);

        if (shipment == null)
            return NotFound();

        return View(shipment);
    }

    // POST: /Seller/Shipments/ConfirmShipment
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmShipment(
        int shipmentId,
        int orderId, 
        List<int> itemIds, 
        string courierName, 
        string trackingNumber,
        decimal weight,
        string packageType,
        int numberOfBoxes,
        decimal? length = null,
        decimal? width = null,
        decimal? height = null)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");
        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);

        if (seller == null)
            return RedirectToAction("Register", "Seller");

        try
        {
            // 1. Get the existing shipment
            var shipment = await _context.Shipments
                .Include(s => s.TrackingEvents)
                .FirstOrDefaultAsync(s => s.Id == shipmentId && s.SellerId == seller.Id);

            if (shipment == null)
            {
                TempData["Error"] = "Shipment not found.";
                return RedirectToAction("Index");
            }

            // 2. Update with package details
            shipment.CourierName = courierName;
            shipment.TrackingNumber = trackingNumber;
            shipment.WeightKg = weight;
            shipment.LengthCm = length;
            shipment.WidthCm = width;
            shipment.HeightCm = height;
            shipment.PackageType = packageType;
            shipment.NumberOfBoxes = numberOfBoxes;
            shipment.InternalNotes = $"Packed in {numberOfBoxes} {packageType}(s). Dims: {length}x{width}x{height} cm.";

            // 3. Mark as Shipped (Amazon style usually implies confirming shipment = shipped)
            // But if we want a "Packed" state first, we can toggle that. 
            // For now, let's assume "Confirm Shipment" means handing over/ready for pickup -> Shipped.
            shipment.Status = ShipmentStatus.Shipped;
            shipment.ShippedAt = DateTime.UtcNow;
            shipment.StatusMessage = "Shipment confirmed and ready for pickup.";

            // 4. Add tracking event
            shipment.TrackingEvents.Add(new ShipmentTrackingEvent
            {
                Status = "Shipped",
                NormalizedStatus = ShipmentStatus.Shipped,
                Message = "Shipment confirmed by seller. Package details recorded.",
                Location = "Seller Warehouse",
                EventTime = DateTime.UtcNow,
                CustomerNotified = true
            });

            // 5. Create PickupRequest for Admin Logistics integration
            // This allows admin to assign a rider to pick up the package from seller
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                // Check if pickup request already exists
                var existingPickup = await _context.Set<PickupRequest>()
                    .FirstOrDefaultAsync(p => p.OrderId == orderId && p.SellerId == seller.Id);
                
                if (existingPickup == null) 
                {
                    var pickupRequest = new PickupRequest
                    {
                        PickupNumber = $"PU-{DateTime.UtcNow:yyyyMMddHHmmss}-{seller.Id}",
                        OrderId = orderId,
                        SellerId = seller.Id,
                        Status = PickupStatus.Scheduled,
                        ScheduledAt = DateTime.UtcNow.AddHours(2), // Default: pickup in 2 hours
                        PickupAddress = seller.BusinessAddress ?? "",
                        PickupPhone = seller.MobileNumber ?? "",
                        Notes = $"Shipment {shipment.ShipmentNumber} ready for pickup. {numberOfBoxes} {packageType}(s), {weight}kg",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Set<PickupRequest>().Add(pickupRequest);
                }
            }

            await _context.SaveChangesAsync();
            
            TempData["Success"] = $"Shipment {shipment.ShipmentNumber} confirmed successfully!";
            return RedirectToAction("Index", new { status = "Shipped" });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error confirming shipment: {ex.Message}";
            return RedirectToAction("Index");
        }
    }

    // GET: /Seller/Shipments/Create?orderId=123 (Legacy, kept for backup)
    public async Task<IActionResult> Create(int orderId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");
        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);

        if (seller == null)
            return RedirectToAction("Register", "Seller");

        var order = await _context.Orders
            .Include(o => o.OrderItems)
                .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order == null)
            return NotFound();

        // Get only items that belong to this seller and not yet shipped
        var sellerItems = order.OrderItems
            .Where(oi => oi.SellerId == seller.Id)
            .ToList();

        if (!sellerItems.Any())
            return NotFound("No items found for this seller in this order");

        // Check if already shipped
        var existingShipments = await _context.Shipments
            .Include(s => s.Items)
            .Where(s => s.OrderId == orderId && s.SellerId == seller.Id)
            .ToListAsync();

        var shippedItemIds = existingShipments
            .SelectMany(s => s.Items.Select(i => i.OrderItemId))
            .ToHashSet();

        var unshippedItems = sellerItems
            .Where(oi => !shippedItemIds.Contains(oi.Id))
            .ToList();

        if (!unshippedItems.Any())
        {
            TempData["Warning"] = "All items in this order have already been shipped.";
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }

        ViewBag.Order = order;
        ViewBag.UnshippedItems = unshippedItems;
        ViewBag.Couriers = new[] { "Pathao", "Steadfast", "Redx", "Sundarban", "SA Paribahan", "Other" };

        return View();
    }

    // POST: /Seller/Shipments/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int orderId, List<int> itemIds, string courierName, string? trackingNumber, string? notes)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");
        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);

        if (seller == null)
            return RedirectToAction("Register", "Seller");

        if (!itemIds.Any())
        {
            TempData["Error"] = "Please select at least one item to ship.";
            return RedirectToAction("Create", new { orderId });
        }

        try
        {
            var shipment = await _shipmentService.CreateShipmentAsync(
                orderId: orderId,
                sellerId: seller.Id,
                orderItemIds: itemIds,
                courierName: courierName,
                trackingNumber: trackingNumber
            );

            if (!string.IsNullOrEmpty(notes))
            {
                shipment.InternalNotes = notes;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"Shipment {shipment.ShipmentNumber} created successfully!";
            return RedirectToAction("Details", new { id = shipment.Id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error creating shipment: {ex.Message}";
            return RedirectToAction("Create", new { orderId });
        }
    }

    // GET: /Seller/Shipments/UpdateStatus/5
    public async Task<IActionResult> UpdateStatus(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");
        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);

        if (seller == null)
            return RedirectToAction("Register", "Seller");

        var shipment = await _context.Shipments
            .Include(s => s.Order)
            .FirstOrDefaultAsync(s => s.Id == id && s.SellerId == seller.Id);

        if (shipment == null)
            return NotFound();

        return View(shipment);
    }

    // POST: /Seller/Shipments/UpdateStatus
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, ShipmentStatus status, string message, string? location, string? trackingNumber)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");
        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);

        if (seller == null)
            return RedirectToAction("Register", "Seller");

        var shipment = await _context.Shipments
            .FirstOrDefaultAsync(s => s.Id == id && s.SellerId == seller.Id);

        if (shipment == null)
            return NotFound();

        try
        {
            // Update tracking number if provided
            if (!string.IsNullOrEmpty(trackingNumber) && trackingNumber != shipment.TrackingNumber)
            {
                await _shipmentService.UpdateTrackingInfoAsync(id, trackingNumber);
            }

            // Update status
            await _shipmentService.UpdateShipmentStatusAsync(
                shipmentId: id,
                status: status,
                message: message,
                location: location,
                userId: user.Id
            );

            TempData["Success"] = "Shipment status updated successfully!";
            return RedirectToAction("Details", new { id });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Error updating status: {ex.Message}";
            return RedirectToAction("UpdateStatus", new { id });
        }
    }

    // POST: /Seller/Shipments/AddTracking
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTracking(int id, string trackingNumber, string? trackingUrl)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");
        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);

        if (seller == null)
            return Json(new { success = false, message = "Seller not found" });

        var shipment = await _context.Shipments
            .FirstOrDefaultAsync(s => s.Id == id && s.SellerId == seller.Id);

        if (shipment == null)
            return Json(new { success = false, message = "Shipment not found" });

        try
        {
            await _shipmentService.UpdateTrackingInfoAsync(id, trackingNumber, null, trackingUrl);
            return Json(new { success = true, message = "Tracking information updated successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    // GET: /Seller/Shipments/PrintLabel/5
    public async Task<IActionResult> PrintLabel(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");
        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);

        if (seller == null)
            return RedirectToAction("Register", "Seller");

        var shipment = await _context.Shipments
            .Include(s => s.Order)
            .Include(s => s.Seller)
            .Include(s => s.Items)
                .ThenInclude(si => si.OrderItem)
                    .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(s => s.Id == id && s.SellerId == seller.Id);

        if (shipment == null)
            return NotFound();

        return View(shipment);
    }
}
