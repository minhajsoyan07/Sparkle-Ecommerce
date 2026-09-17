using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Logistics;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class LogisticsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly Sparkle.Infrastructure.Services.ILogisticsService _logisticsService;
    private readonly Sparkle.Infrastructure.Services.IWalletService _walletService;

    public LogisticsController(ApplicationDbContext db, Sparkle.Infrastructure.Services.ILogisticsService logisticsService, Sparkle.Infrastructure.Services.IWalletService walletService)
    {
        _db = db;
        _logisticsService = logisticsService;
        _walletService = walletService;
    }

    #region Dashboard
    
    public async Task<IActionResult> Index()
    {
        var totalHubs = await _db.Hubs.CountAsync(h => !h.IsDeleted);
        var activeHubs = await _db.Hubs.CountAsync(h => !h.IsDeleted && h.OperationalStatus == HubOperationalStatus.Operational);
        var totalRiders = await _db.Riders.CountAsync(r => !r.IsDeleted);
        var activeRiders = await _db.Riders.CountAsync(r => !r.IsDeleted && r.Status == RiderStatus.Active);
        var pendingPickups = await _db.PickupRequests.CountAsync(p => !p.IsDeleted && p.Status == PickupStatus.Scheduled);
        var inTransitDeliveries = await _db.DeliveryAssignments.CountAsync(d => !d.IsDeleted && d.Status == DeliveryStatus.InTransit);

        var stats = new
        {
            TotalHubs = totalHubs,
            ActiveHubs = activeHubs,
            TotalRiders = totalRiders,
            ActiveRiders = activeRiders,
            PendingPickups = pendingPickups,
            InTransitDeliveries = inTransitDeliveries
        };

        ViewBag.Stats = stats;
        return View();
    }
    
    #endregion
    
    #region Hubs
    
    public async Task<IActionResult> Hubs(string type = "all", string status = "all", string q = "", int page = 1)
    {
        var query = _db.Hubs.AsNoTracking().Where(h => !h.IsDeleted).AsQueryable();
        
        // Search
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim().ToLower();
            query = query.Where(h => h.Name.ToLower().Contains(q) || h.HubCode.ToLower().Contains(q) || h.District.ToLower().Contains(q));
        }
        
        // Filter by type
        if (type != "all" && Enum.TryParse<HubType>(type, true, out var hubType))
        {
            query = query.Where(h => h.Type == hubType);
        }
        
        // Filter by status
        if (status != "all" && Enum.TryParse<HubOperationalStatus>(status, true, out var hubStatus))
        {
            query = query.Where(h => h.OperationalStatus == hubStatus);
        }
        
        // Pagination
        int pageSize = 15;
        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));
        
        var hubs = await query
            .OrderBy(h => h.Type)
            .ThenBy(h => h.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        ViewBag.Type = type;
        ViewBag.Status = status;
        ViewBag.SearchQuery = q;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        
        return View(hubs);
    }
    
    public async Task<IActionResult> HubDetails(int id)
    {
        var hub = await _db.Hubs
            .Include(h => h.AssignedRiders.Where(r => !r.IsDeleted))
            .Include(h => h.Inventory.Where(i => !i.IsDeleted))
                .ThenInclude(i => i.Order)
            .FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
            
        if (hub == null) return NotFound();
        
        return View(hub);
    }
    
    public IActionResult CreateHub()
    {
        return View();
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateHub(Hub model)
    {
        if (await _db.Hubs.AnyAsync(h => h.HubCode == model.HubCode && !h.IsDeleted))
        {
            ModelState.AddModelError("HubCode", "Hub code already exists");
        }
        
        if (!ModelState.IsValid) return View(model);
        
        model.CreatedAt = DateTime.UtcNow;
        model.CreatedBy = User.Identity?.Name;
        
        _db.Hubs.Add(model);
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"Hub '{model.Name}' created successfully.";
        return RedirectToAction(nameof(Hubs));
    }
    
    public async Task<IActionResult> EditHub(int id)
    {
        var hub = await _db.Hubs.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
        if (hub == null) return NotFound();
        
        return View(hub);
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditHub(int id, Hub model)
    {
        var hub = await _db.Hubs.FirstOrDefaultAsync(h => h.Id == id && !h.IsDeleted);
        if (hub == null) return NotFound();
        
        if (await _db.Hubs.AnyAsync(h => h.HubCode == model.HubCode && h.Id != id && !h.IsDeleted))
        {
            ModelState.AddModelError("HubCode", "Hub code already exists");
        }
        
        if (!ModelState.IsValid) return View(model);
        
        hub.Name = model.Name;
        hub.HubCode = model.HubCode;
        hub.Type = model.Type;
        hub.Address = model.Address;
        hub.Area = model.Area;
        hub.District = model.District;
        hub.Division = model.Division;
        hub.PostalCode = model.PostalCode;
        hub.Latitude = model.Latitude;
        hub.Longitude = model.Longitude;
        hub.Capacity = model.Capacity;
        hub.OperationalStatus = model.OperationalStatus;
        hub.ContactPhone = model.ContactPhone;
        hub.ContactEmail = model.ContactEmail;
        hub.ManagerName = model.ManagerName;
        hub.ManagerPhone = model.ManagerPhone;
        hub.UpdatedAt = DateTime.UtcNow;
        hub.UpdatedBy = User.Identity?.Name;
        
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"Hub '{hub.Name}' updated successfully.";
        return RedirectToAction(nameof(HubDetails), new { id });
    }
    
    [HttpPost]
    public async Task<IActionResult> ToggleHubStatus(int hubId)
    {
        var hub = await _db.Hubs.FirstOrDefaultAsync(h => h.Id == hubId && !h.IsDeleted);
        if (hub == null) return Json(new { success = false, message = "Hub not found" });
        
        hub.OperationalStatus = hub.OperationalStatus == HubOperationalStatus.Operational 
            ? HubOperationalStatus.Maintenance 
            : HubOperationalStatus.Operational;
        hub.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        return Json(new { success = true, status = hub.OperationalStatus.ToString() });
    }
    
    #endregion
    
    #region Riders
    
    public async Task<IActionResult> Riders(string status = "all", string type = "all", int? hubId = null, string q = "", int page = 1)
    {
        var query = _db.Riders.AsNoTracking().Include(r => r.AssignedHub).Where(r => !r.IsDeleted).AsQueryable();
        
        // Search
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim().ToLower();
            query = query.Where(r => r.Name.ToLower().Contains(q) || r.Phone.Contains(q) || r.RiderCode.ToLower().Contains(q));
        }
        
        // Filter by status
        if (status != "all" && Enum.TryParse<RiderStatus>(status, true, out var riderStatus))
        {
            query = query.Where(r => r.Status == riderStatus);
        }
        
        // Filter by type
        if (type != "all" && Enum.TryParse<RiderType>(type, true, out var riderType))
        {
            query = query.Where(r => r.Type == riderType);
        }
        
        // Filter by hub
        if (hubId.HasValue)
        {
            query = query.Where(r => r.AssignedHubId == hubId.Value);
        }
        
        // Pagination
        int pageSize = 20;
        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));
        
        var riders = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        ViewBag.Hubs = await _db.Hubs.Where(h => !h.IsDeleted && h.OperationalStatus == HubOperationalStatus.Operational).ToListAsync();
        ViewBag.Status = status;
        ViewBag.Type = type;
        ViewBag.HubId = hubId;
        ViewBag.SearchQuery = q;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        
        return View(riders);
    }
    
    public async Task<IActionResult> RiderDetails(int id)
    {
        var rider = await _db.Riders
            .Include(r => r.AssignedHub)
            .Include(r => r.PickupRequests.Where(p => !p.IsDeleted).OrderByDescending(p => p.CreatedAt).Take(10))
            .Include(r => r.DeliveryAssignments.Where(d => !d.IsDeleted).OrderByDescending(d => d.CreatedAt).Take(10))
            .FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
            
        if (rider == null) return NotFound();
        
        return View(rider);
    }
    
    public async Task<IActionResult> CreateRider()
    {
        ViewBag.Hubs = await _db.Hubs.Where(h => !h.IsDeleted && h.OperationalStatus == HubOperationalStatus.Operational).ToListAsync();
        return View();
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRider(Rider model)
    {
        if (await _db.Riders.AnyAsync(r => r.RiderCode == model.RiderCode && !r.IsDeleted))
        {
            ModelState.AddModelError("RiderCode", "Rider code already exists");
        }
        
        if (!ModelState.IsValid)
        {
            ViewBag.Hubs = await _db.Hubs.Where(h => !h.IsDeleted).ToListAsync();
            return View(model);
        }
        
        model.CreatedAt = DateTime.UtcNow;
        model.CreatedBy = User.Identity?.Name;
        
        _db.Riders.Add(model);
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"Rider '{model.Name}' created successfully.";
        return RedirectToAction(nameof(Riders));
    }
    
    public async Task<IActionResult> EditRider(int id)
    {
        var rider = await _db.Riders.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        if (rider == null) return NotFound();
        
        ViewBag.Hubs = await _db.Hubs.Where(h => !h.IsDeleted).ToListAsync();
        return View(rider);
    }
    
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditRider(int id, Rider model)
    {
        var rider = await _db.Riders.FirstOrDefaultAsync(r => r.Id == id && !r.IsDeleted);
        if (rider == null) return NotFound();
        
        if (await _db.Riders.AnyAsync(r => r.RiderCode == model.RiderCode && r.Id != id && !r.IsDeleted))
        {
            ModelState.AddModelError("RiderCode", "Rider code already exists");
        }
        
        if (!ModelState.IsValid)
        {
            ViewBag.Hubs = await _db.Hubs.Where(h => !h.IsDeleted).ToListAsync();
            return View(model);
        }
        
        rider.Name = model.Name;
        rider.RiderCode = model.RiderCode;
        rider.Phone = model.Phone;
        rider.Email = model.Email;
        rider.NidNumber = model.NidNumber;
        rider.VehicleType = model.VehicleType;
        rider.VehicleNumber = model.VehicleNumber;
        rider.AssignedHubId = model.AssignedHubId;
        rider.Type = model.Type;
        rider.Status = model.Status;
        rider.UpdatedAt = DateTime.UtcNow;
        rider.UpdatedBy = User.Identity?.Name;
        
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"Rider '{rider.Name}' updated successfully.";
        return RedirectToAction(nameof(RiderDetails), new { id });
    }
    
    [HttpPost]
    public async Task<IActionResult> ToggleRiderStatus(int riderId)
    {
        var rider = await _db.Riders.FirstOrDefaultAsync(r => r.Id == riderId && !r.IsDeleted);
        if (rider == null) return Json(new { success = false, message = "Rider not found" });
        
        rider.Status = rider.Status == RiderStatus.Active ? RiderStatus.Inactive : RiderStatus.Active;
        rider.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        return Json(new { success = true, status = rider.Status.ToString() });
    }
    
    #endregion
    
    #region Pickups
    
    public async Task<IActionResult> Pickups(string status = "all", int page = 1)
    {
        var query = _db.PickupRequests
            .AsNoTracking()
            .Include(p => p.AssignedRider)
            .Include(p => p.DestinationHub)
            .Include(p => p.Seller)
            .Where(p => !p.IsDeleted)
            .AsQueryable();
        
        if (status != "all" && Enum.TryParse<PickupStatus>(status, true, out var pickupStatus))
        {
            query = query.Where(p => p.Status == pickupStatus);
        }
        
        int pageSize = 20;
        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));
        
        var pickups = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        
        return View(pickups);
    }
    
    [HttpPost]
    public async Task<IActionResult> AssignPickupRider(int pickupId, int riderId)
    {
        var pickup = await _db.PickupRequests.FirstOrDefaultAsync(p => p.Id == pickupId && !p.IsDeleted);
        var rider = await _db.Riders.FirstOrDefaultAsync(r => r.Id == riderId && !r.IsDeleted);
        
        if (pickup == null) return Json(new { success = false, message = "Pickup not found" });
        if (rider == null) return Json(new { success = false, message = "Rider not found" });
        
        pickup.AssignedRiderId = riderId;
        pickup.AssignedAt = DateTime.UtcNow;
        pickup.Status = PickupStatus.Assigned;
        pickup.UpdatedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        return Json(new { success = true, message = $"Pickup assigned to {rider.Name}" });
    }
    
    #endregion
    
    #region Deliveries
    
    public async Task<IActionResult> Deliveries(string status = "all", int page = 1)
    {
        var query = _db.DeliveryAssignments
            .AsNoTracking()
            .Include(d => d.Rider)
            .Include(d => d.SourceHub)
            .Include(d => d.Order)
            .Where(d => !d.IsDeleted)
            .AsQueryable();
        
        if (status != "all" && Enum.TryParse<DeliveryStatus>(status, true, out var deliveryStatus))
        {
            query = query.Where(d => d.Status == deliveryStatus);
        }
        
        int pageSize = 20;
        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, Math.Max(1, totalPages)));
        
        var deliveries = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        ViewBag.Status = status;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        
        return View(deliveries);
    }
    
    #endregion

    #region Operational Actions (Mocking Rider App)

    [HttpPost]
    public async Task<IActionResult> MarkPickedUp(int pickupId)
    {
        await _logisticsService.UpdatePickupStatusAsync(pickupId, PickupStatus.InProgress, "Picked up by rider");
        // Simulate immediate arrival at hub for simplicity if needed, or separate step
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> MarkArrivedAtHub(int pickupId)
    {
        var pickup = await _db.PickupRequests.Include(p => p.AssignedRider).FirstOrDefaultAsync(p => p.Id == pickupId);
        if (pickup == null) return Json(new { success = false, message = "Not found" });

        await _logisticsService.UpdatePickupStatusAsync(pickupId, PickupStatus.Completed, "Delivered to Hub");
        
        // Auto-receive at hub logic
        int? targetHubId = pickup.DestinationHubId ?? pickup.AssignedRider?.AssignedHubId;
        
        if (targetHubId.HasValue) 
        {
             await _logisticsService.ReceiveAtHubAsync(targetHubId.Value, pickup.OrderId);
        }
        else
        {
            // Fallback: Find a Central Hub
            var centralHub = await _db.Hubs.FirstOrDefaultAsync(h => h.Type == HubType.Central && !h.IsDeleted);
            if (centralHub != null)
            {
                await _logisticsService.ReceiveAtHubAsync(centralHub.Id, pickup.OrderId);
            }
        }
        
        return Json(new { success = true });
    }

    [HttpPost]
    public async Task<IActionResult> CreateDelivery(int orderId, int riderId, int sourceHubId)
    {
        try 
        {
            await _logisticsService.CreateDeliveryAssignmentAsync(orderId, riderId, sourceHubId);
            return Json(new { success = true });
        }
        catch(Exception ex) 
        {
             return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> MarkDelivered(int deliveryId)
    {
        await _logisticsService.CompleteDeliveryAsync(deliveryId);
        return Json(new { success = true });
    }

    #endregion
}
