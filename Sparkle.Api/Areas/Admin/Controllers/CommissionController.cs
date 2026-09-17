using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Sparkle.Infrastructure;
using Sparkle.Domain.Configuration;
using System.Text.Json;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CommissionController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<CommissionController> _logger;

    public CommissionController(ApplicationDbContext db, ILogger<CommissionController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // GET: Admin/Commission
    public async Task<IActionResult> Index()
    {
        var currentConfig = await _db.CommissionConfigs
            .Where(c => c.IsActive)
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (currentConfig == null)
        {
            // Create default configuration
            currentConfig = new CommissionConfig
            {
                GlobalRate = 15.0m,
                CategoryRates = "{}",
                SellerRates = "{}",
                EffectiveFrom = DateTime.UtcNow,
                IsActive = true,
                Notes = "Default commission configuration"
            };
            _db.CommissionConfigs.Add(currentConfig);
            await _db.SaveChangesAsync();
        }

        // Get categories for dropdown
        ViewBag.Categories = await _db.Categories
            .OrderBy(c => c.Name)
            .ToListAsync();

        // Get sellers for dropdown
        ViewBag.Sellers = await _db.Sellers
            .Where(s => s.Status == Sparkle.Domain.Sellers.SellerStatus.Approved)
            .OrderBy(s => s.ShopName)
            .ToListAsync();

        return View(currentConfig);
    }

    // POST: Admin/Commission/UpdateGlobal
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGlobal(decimal globalRate, string? notes)
    {
        if (globalRate < 0 || globalRate > 100)
        {
            TempData["Error"] = "Commission rate must be between 0% and 100%";
            return RedirectToAction(nameof(Index));
        }

        var currentConfig = await _db.CommissionConfigs
            .Where(c => c.IsActive)
            .FirstOrDefaultAsync();

        if (currentConfig != null)
        {
            currentConfig.GlobalRate = globalRate;
            currentConfig.Notes = notes;
            currentConfig.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Success"] = "Global commission rate updated successfully";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/Commission/SetCategoryRate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCategoryRate(int categoryId, decimal rate)
    {
        if (rate < 0 || rate > 100)
        {
            return Json(new { success = false, message = "Rate must be between 0% and 100%" });
        }

        var currentConfig = await _db.CommissionConfigs
            .Where(c => c.IsActive)
            .FirstOrDefaultAsync();

        if (currentConfig != null)
        {
            var categoryRates = string.IsNullOrEmpty(currentConfig.CategoryRates) 
                ? new Dictionary<string, decimal>()
                : JsonSerializer.Deserialize<Dictionary<string, decimal>>(currentConfig.CategoryRates) 
                ?? new Dictionary<string, decimal>();

            categoryRates[categoryId.ToString()] = rate;
            currentConfig.CategoryRates = JsonSerializer.Serialize(categoryRates);
            currentConfig.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Category rate updated" });
        }

        return Json(new { success = false, message = "Configuration not found" });
    }

    // POST: Admin/Commission/SetSellerRate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSellerRate(int sellerId, decimal rate)
    {
        if (rate < 0 || rate > 100)
        {
            return Json(new { success = false, message = "Rate must be between 0% and 100%" });
        }

        var currentConfig = await _db.CommissionConfigs
            .Where(c => c.IsActive)
            .FirstOrDefaultAsync();

        if (currentConfig != null)
        {
            var sellerRates = string.IsNullOrEmpty(currentConfig.SellerRates)
                ? new Dictionary<string, decimal>()
                : JsonSerializer.Deserialize<Dictionary<string, decimal>>(currentConfig.SellerRates)
                ?? new Dictionary<string, decimal>();

            sellerRates[sellerId.ToString()] = rate;
            currentConfig.SellerRates = JsonSerializer.Serialize(sellerRates);
            currentConfig.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Seller rate updated" });
        }

        return Json(new { success = false, message = "Configuration not found" });
    }

    // POST: Admin/Commission/RemoveCategoryRate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCategoryRate(int categoryId)
    {
        var currentConfig = await _db.CommissionConfigs
            .Where(c => c.IsActive)
            .FirstOrDefaultAsync();

        if (currentConfig != null)
        {
            var categoryRates = JsonSerializer.Deserialize<Dictionary<string, decimal>>(currentConfig.CategoryRates)
                ?? new Dictionary<string, decimal>();

            categoryRates.Remove(categoryId.ToString());
            currentConfig.CategoryRates = JsonSerializer.Serialize(categoryRates);
            currentConfig.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        return Json(new { success = false });
    }

    // POST: Admin/Commission/RemoveSellerRate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSellerRate(int sellerId)
    {
        var currentConfig = await _db.CommissionConfigs
            .Where(c => c.IsActive)
            .FirstOrDefaultAsync();

        if (currentConfig != null)
        {
            var sellerRates = JsonSerializer.Deserialize<Dictionary<string, decimal>>(currentConfig.SellerRates)
                ?? new Dictionary<string, decimal>();

            sellerRates.Remove(sellerId.ToString());
            currentConfig.SellerRates = JsonSerializer.Serialize(sellerRates);
            currentConfig.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true });
        }

        return Json(new { success = false });
    }
}
