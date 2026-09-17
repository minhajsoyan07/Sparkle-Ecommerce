using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Sellers;

namespace Sparkle.Api.Areas.Seller.Controllers;

[Area("Seller")]
[Authorize(Roles = "Seller")]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _db;

    public SettingsController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        
        if (seller == null) return RedirectToAction("Index", "Dashboard");

        return View(seller);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateSettings(Sparkle.Domain.Sellers.Seller model)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);

        if (seller == null) return NotFound();

        // Update Allowed Fields
        seller.ShopName = model.ShopName;
        seller.ShopDescription = model.ShopDescription;
        seller.MobileNumber = model.MobileNumber;
        seller.BkashMerchantNumber = model.BkashMerchantNumber;
        seller.BusinessAddress = model.BusinessAddress;
        
        _db.Sellers.Update(seller);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Settings updated successfully";
        return RedirectToAction("Index");
    }
}
