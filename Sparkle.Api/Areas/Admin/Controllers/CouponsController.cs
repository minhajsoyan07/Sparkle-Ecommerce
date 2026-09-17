using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Marketing;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CouponsController : Controller
{
    private readonly ApplicationDbContext _db;

    public CouponsController(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// List all coupons with search, filter, pagination
    /// </summary>
    public async Task<IActionResult> Index(string q = "", string status = "all", int page = 1)
    {
        var pageSize = 20;
        var query = _db.Coupons.AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(c => c.Code.ToLower().Contains(term) || c.Name.ToLower().Contains(term));
            ViewBag.SearchQuery = q;
        }

        // Filter by status
        var now = DateTime.UtcNow;
        if (status == "active")
        {
            query = query.Where(c => c.IsActive && c.StartDate <= now && c.EndDate >= now);
        }
        else if (status == "expired")
        {
            query = query.Where(c => c.EndDate < now);
        }
        else if (status == "scheduled")
        {
            query = query.Where(c => c.StartDate > now);
        }
        else if (status == "disabled")
        {
            query = query.Where(c => !c.IsActive);
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var coupons = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Counts for tabs
        ViewBag.AllCount = await _db.Coupons.CountAsync();
        ViewBag.ActiveCount = await _db.Coupons.CountAsync(c => c.IsActive && c.StartDate <= now && c.EndDate >= now);
        ViewBag.ScheduledCount = await _db.Coupons.CountAsync(c => c.StartDate > now);
        ViewBag.ExpiredCount = await _db.Coupons.CountAsync(c => c.EndDate < now);
        ViewBag.DisabledCount = await _db.Coupons.CountAsync(c => !c.IsActive);

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentStatus = status;

        return View(coupons);
    }

    /// <summary>
    /// Show create coupon form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Sellers = await _db.Sellers.Where(s => s.Status == Sparkle.Domain.Sellers.SellerStatus.Approved).OrderBy(s => s.ShopName).ToListAsync();
        
        return View(new Coupon 
        { 
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(1),
            Code = GenerateCouponCode()
        });
    }

    /// <summary>
    /// Create a new coupon
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Coupon coupon)
    {
        // Validate code uniqueness
        if (await _db.Coupons.AnyAsync(c => c.Code.ToLower() == coupon.Code.ToLower()))
        {
            ModelState.AddModelError("Code", "This coupon code already exists.");
        }

        // Validate dates
        if (coupon.EndDate <= coupon.StartDate)
        {
            ModelState.AddModelError("EndDate", "End date must be after start date.");
        }

        // Validate discount
        if (coupon.DiscountType == "Percentage" && (coupon.DiscountValue <= 0 || coupon.DiscountValue > 100))
        {
            ModelState.AddModelError("DiscountValue", "Percentage must be between 1 and 100.");
        }
        else if (coupon.DiscountType == "FixedAmount" && coupon.DiscountValue <= 0)
        {
            ModelState.AddModelError("DiscountValue", "Amount must be greater than 0.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Sellers = await _db.Sellers.Where(s => s.Status == Sparkle.Domain.Sellers.SellerStatus.Approved).OrderBy(s => s.ShopName).ToListAsync();
            return View(coupon);
        }

        _db.Coupons.Add(coupon);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Coupon '{coupon.Code}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Show edit coupon form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null)
        {
            return NotFound();
        }

        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        ViewBag.Sellers = await _db.Sellers.Where(s => s.Status == Sparkle.Domain.Sellers.SellerStatus.Approved).OrderBy(s => s.ShopName).ToListAsync();
        ViewBag.UsageCount = await _db.Set<VoucherUsage>().CountAsync(v => v.CouponId == id);
        
        return View(coupon);
    }

    /// <summary>
    /// Update an existing coupon
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Coupon coupon)
    {
        if (id != coupon.Id)
        {
            return NotFound();
        }

        // Validate code uniqueness (excluding current)
        if (await _db.Coupons.AnyAsync(c => c.Code.ToLower() == coupon.Code.ToLower() && c.Id != id))
        {
            ModelState.AddModelError("Code", "This coupon code already exists.");
        }

        // Validate dates
        if (coupon.EndDate <= coupon.StartDate)
        {
            ModelState.AddModelError("EndDate", "End date must be after start date.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
            ViewBag.Sellers = await _db.Sellers.Where(s => s.Status == Sparkle.Domain.Sellers.SellerStatus.Approved).OrderBy(s => s.ShopName).ToListAsync();
            return View(coupon);
        }

        var existingCoupon = await _db.Coupons.FindAsync(id);
        if (existingCoupon == null)
        {
            return NotFound();
        }

        // Update fields
        existingCoupon.Code = coupon.Code;
        existingCoupon.Name = coupon.Name;
        existingCoupon.Description = coupon.Description;
        existingCoupon.DiscountType = coupon.DiscountType;
        existingCoupon.DiscountValue = coupon.DiscountValue;
        existingCoupon.MaxDiscountAmount = coupon.MaxDiscountAmount;
        existingCoupon.MinimumPurchaseAmount = coupon.MinimumPurchaseAmount;
        existingCoupon.MaxUsageTotal = coupon.MaxUsageTotal;
        existingCoupon.MaxUsagePerUser = coupon.MaxUsagePerUser;
        existingCoupon.StartDate = coupon.StartDate;
        existingCoupon.EndDate = coupon.EndDate;
        existingCoupon.IsActive = coupon.IsActive;
        existingCoupon.IsPublic = coupon.IsPublic;
        existingCoupon.ApplicableCategories = coupon.ApplicableCategories;
        existingCoupon.SellerId = coupon.SellerId;
        existingCoupon.TargetUserSegment = coupon.TargetUserSegment;

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Coupon '{coupon.Code}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Toggle coupon active status
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null)
        {
            return NotFound();
        }

        coupon.IsActive = !coupon.IsActive;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Coupon '{coupon.Code}' has been {(coupon.IsActive ? "activated" : "deactivated")}.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Delete a coupon
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null)
        {
            return NotFound();
        }

        // Check if coupon has been used
        var usageCount = await _db.Set<VoucherUsage>().CountAsync(v => v.CouponId == id);
        if (usageCount > 0)
        {
            TempData["Error"] = $"Cannot delete coupon '{coupon.Code}' because it has been used {usageCount} times. Deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        _db.Coupons.Remove(coupon);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Coupon '{coupon.Code}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// View coupon usage history
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Usage(int id, int page = 1)
    {
        var coupon = await _db.Coupons.FindAsync(id);
        if (coupon == null)
        {
            return NotFound();
        }

        var pageSize = 20;
        var usages = await _db.Set<VoucherUsage>()
            .Include(v => v.User)
            .Include(v => v.Order)
            .Where(v => v.CouponId == id)
            .OrderByDescending(v => v.UsedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var totalUsages = await _db.Set<VoucherUsage>().CountAsync(v => v.CouponId == id);
        var totalDiscount = await _db.Set<VoucherUsage>().Where(v => v.CouponId == id).SumAsync(v => v.DiscountAmount);

        ViewBag.Coupon = coupon;
        ViewBag.TotalUsages = totalUsages;
        ViewBag.TotalDiscount = totalDiscount;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling(totalUsages / (double)pageSize);

        return View(usages);
    }

    // Helper to generate random coupon code
    private string GenerateCouponCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var random = new Random();
        return new string(Enumerable.Repeat(chars, 8).Select(s => s[random.Next(s.Length)]).ToArray());
    }
}
