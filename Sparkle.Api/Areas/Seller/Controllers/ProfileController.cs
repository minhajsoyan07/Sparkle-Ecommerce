using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Areas.Seller.Models;
using Sparkle.Domain.Identity;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Seller.Controllers;

[Area("Seller")]
[Authorize(Roles = "Seller")]
public class ProfileController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ProfileController(
        ApplicationDbContext db, 
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment webHostEnvironment)
    {
        _db = db;
        _userManager = userManager;
        _webHostEnvironment = webHostEnvironment;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException();

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return RedirectToAction("Register", "Seller");

        var model = new SellerProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            ProfilePhotoPath = user.ProfilePhotoPath,
            Gender = user.Gender,
            
            ShopName = seller.ShopName,
            ShopDescription = seller.ShopDescription,
            MobileNumber = seller.MobileNumber,
            BusinessAddress = seller.BusinessAddress,
            City = seller.City,
            District = seller.District
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SellerProfileViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = GetUserId();
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return RedirectToAction("Register", "Seller");

        // Update User Info
        user.FullName = model.FullName;
        user.PhoneNumber = model.PhoneNumber;
        user.Gender = model.Gender;

        // Handle Profile Photo Upload
        if (model.ProfilePhoto != null)
        {
            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "profiles");
            if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

            string uniqueFileName = Guid.NewGuid().ToString() + "_" + model.ProfilePhoto.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await model.ProfilePhoto.CopyToAsync(fileStream);
            }

            // Delete old photo if exists
            if (!string.IsNullOrEmpty(user.ProfilePhotoPath))
            {
                // Logic to delete old file could be added here
            }

            user.ProfilePhotoPath = "/uploads/profiles/" + uniqueFileName;
        }

        // Update Seller Info
        seller.ShopName = model.ShopName;
        seller.ShopDescription = model.ShopDescription;
        seller.MobileNumber = model.MobileNumber;
        seller.BusinessAddress = model.BusinessAddress;
        seller.City = model.City;
        seller.District = model.District;

        _db.Sellers.Update(seller);
        await _userManager.UpdateAsync(user);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Profile updated successfully!";
        return RedirectToAction(nameof(Index), new { area = "Seller" });
    }
}
