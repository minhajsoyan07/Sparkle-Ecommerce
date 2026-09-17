using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Marketing;
using Microsoft.AspNetCore.Authorization;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class BannersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<BannersController> _logger;

    public BannersController(
        ApplicationDbContext db,
        IWebHostEnvironment env,
        ILogger<BannersController> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    // GET: Admin/Banners
    public async Task<IActionResult> Index(string? type, string? position, bool? isActive, string? search)
    {
        var query = _db.Banners.AsQueryable();

        // Filters
        if (!string.IsNullOrEmpty(type))
            query = query.Where(b => b.BannerType == type);

        if (!string.IsNullOrEmpty(position))
            query = query.Where(b => b.Position == position);

        if (isActive.HasValue)
            query = query.Where(b => b.IsActive == isActive.Value);

        if (!string.IsNullOrEmpty(search))
            query = query.Where(b => b.Title.Contains(search) || 
                                    (b.Description != null && b.Description.Contains(search)));

        var banners = await query
            .OrderBy(b => b.DisplayOrder)
            .ThenByDescending(b => b.CreatedAt)
            .ToListAsync();

        // Pass filter values to view
        ViewBag.Type = type;
        ViewBag.Position = position;
        ViewBag.IsActive = isActive;
        ViewBag.Search = search;

        return View(banners);
    }

    // GET: Admin/Banners/Create
    public IActionResult Create()
    {
        var model = new Banner
        {
            IsActive = true,
            DisplayOrder = 0,
            LinkTarget = "_self",
            BannerType = "Hero",
            Position = "Homepage"
        };
        return View(model);
    }

    // POST: Admin/Banners/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Banner banner, 
        IFormFile? imageDesktop, 
        IFormFile? imageMobile, 
        IFormFile? imageTablet)
    {
        if (ModelState.IsValid)
        {
            try
            {
                // Upload images
                if (imageDesktop != null)
                    banner.ImageUrlDesktop = await UploadBannerImage(imageDesktop, "desktop");

                if (imageMobile != null)
                    banner.ImageUrlMobile = await UploadBannerImage(imageMobile, "mobile");

                if (imageTablet != null)
                    banner.ImageUrlTablet = await UploadBannerImage(imageTablet, "tablet");

                // Ensure ImageUrlDesktop is set
                if (string.IsNullOrEmpty(banner.ImageUrlDesktop))
                {
                    ModelState.AddModelError("", "Desktop image is required");
                    return View(banner);
                }

                banner.CreatedAt = DateTime.UtcNow;
                banner.UpdatedAt = DateTime.UtcNow;

                _db.Banners.Add(banner);
                await _db.SaveChangesAsync();

                TempData["Success"] = "Banner created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating banner");
                ModelState.AddModelError("", "An error occurred while creating the banner");
            }
        }

        return View(banner);
    }

    // GET: Admin/Banners/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var banner = await _db.Banners.FindAsync(id);
        if (banner == null)
            return NotFound();

        return View(banner);
    }

    // POST: Admin/Banners/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Banner banner,
        IFormFile? imageDesktop,
        IFormFile? imageMobile,
        IFormFile? imageTablet,
        bool removeDesktop = false,
        bool removeMobile = false,
        bool removeTablet = false)
    {
        if (id != banner.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existingBanner = await _db.Banners.FindAsync(id);
                if (existingBanner == null)
                    return NotFound();

                // Handle image uploads and deletions
                if (removeDesktop && !string.IsNullOrEmpty(existingBanner.ImageUrlDesktop))
                {
                    DeleteBannerImage(existingBanner.ImageUrlDesktop);
                    existingBanner.ImageUrlDesktop = string.Empty;
                }

                if (imageDesktop != null)
                {
                    if (!string.IsNullOrEmpty(existingBanner.ImageUrlDesktop))
                        DeleteBannerImage(existingBanner.ImageUrlDesktop);
                    existingBanner.ImageUrlDesktop = await UploadBannerImage(imageDesktop, "desktop");
                }

                if (removeMobile && !string.IsNullOrEmpty(existingBanner.ImageUrlMobile))
                {
                    DeleteBannerImage(existingBanner.ImageUrlMobile);
                    existingBanner.ImageUrlMobile = null;
                }

                if (imageMobile != null)
                {
                    if (!string.IsNullOrEmpty(existingBanner.ImageUrlMobile))
                        DeleteBannerImage(existingBanner.ImageUrlMobile);
                    existingBanner.ImageUrlMobile = await UploadBannerImage(imageMobile, "mobile");
                }

                if (removeTablet && !string.IsNullOrEmpty(existingBanner.ImageUrlTablet))
                {
                    DeleteBannerImage(existingBanner.ImageUrlTablet);
                    existingBanner.ImageUrlTablet = null;
                }

                if (imageTablet != null)
                {
                    if (!string.IsNullOrEmpty(existingBanner.ImageUrlTablet))
                        DeleteBannerImage(existingBanner.ImageUrlTablet);
                    existingBanner.ImageUrlTablet = await UploadBannerImage(imageTablet, "tablet");
                }

                // Ensure desktop image exists
                if (string.IsNullOrEmpty(existingBanner.ImageUrlDesktop))
                {
                    ModelState.AddModelError("", "Desktop image is required");
                    return View(banner);
                }

                // Update properties
                existingBanner.Title = banner.Title;
                existingBanner.SubTitle = banner.SubTitle;
                existingBanner.Description = banner.Description;
                existingBanner.BannerType = banner.BannerType;
                existingBanner.Position = banner.Position;
                existingBanner.LinkUrl = banner.LinkUrl;
                existingBanner.LinkTarget = banner.LinkTarget;
                existingBanner.ButtonText = banner.ButtonText;
                existingBanner.StartDate = banner.StartDate;
                existingBanner.EndDate = banner.EndDate;
                existingBanner.IsActive = banner.IsActive;
                existingBanner.DisplayOrder = banner.DisplayOrder;
                existingBanner.BackgroundColor = banner.BackgroundColor;
                existingBanner.TextColor = banner.TextColor;
                existingBanner.UpdatedAt = DateTime.UtcNow;

                await _db.SaveChangesAsync();

                TempData["Success"] = "Banner updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating banner {BannerId}", id);
                ModelState.AddModelError("", "An error occurred while updating the banner");
            }
        }

        return View(banner);
    }

    // POST: Admin/Banners/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var banner = await _db.Banners.FindAsync(id);
            if (banner == null)
                return NotFound();

            // Delete images
            if (!string.IsNullOrEmpty(banner.ImageUrlDesktop))
                DeleteBannerImage(banner.ImageUrlDesktop);
            if (!string.IsNullOrEmpty(banner.ImageUrlMobile))
                DeleteBannerImage(banner.ImageUrlMobile);
            if (!string.IsNullOrEmpty(banner.ImageUrlTablet))
                DeleteBannerImage(banner.ImageUrlTablet);

            _db.Banners.Remove(banner);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Banner deleted successfully!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting banner {BannerId}", id);
            TempData["Error"] = "An error occurred while deleting the banner";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: Admin/Banners/ToggleStatus/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id)
    {
        try
        {
            var banner = await _db.Banners.FindAsync(id);
            if (banner == null)
                return Json(new { success = false, message = "Banner not found" });

            banner.IsActive = !banner.IsActive;
            banner.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Json(new { success = true, isActive = banner.IsActive });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error toggling banner status {BannerId}", id);
            return Json(new { success = false, message = "An error occurred" });
        }
    }

    // POST: Admin/Banners/Reorder
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reorder([FromBody] List<int> bannerIds)
    {
        try
        {
            for (int i = 0; i < bannerIds.Count; i++)
            {
                var banner = await _db.Banners.FindAsync(bannerIds[i]);
                if (banner != null)
                {
                    banner.DisplayOrder = i;
                    banner.UpdatedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reordering banners");
            return Json(new { success = false, message = "An error occurred" });
        }
    }

    // Helper Methods
    private async Task<string> UploadBannerImage(IFormFile file, string type)
    {
        var uploadsFolder = Path.Combine(_env.WebRootPath, "uploads", "banners");
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid()}_{type}_{file.FileName}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(fileStream);
        }

        return $"/uploads/banners/{uniqueFileName}";
    }

    private void DeleteBannerImage(string imagePath)
    {
        try
        {
            var fullPath = Path.Combine(_env.WebRootPath, imagePath.TrimStart('/'));
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting image {ImagePath}", imagePath);
        }
    }
}
