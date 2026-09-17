using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Catalog;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class BrandsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _env;

    public BrandsController(ApplicationDbContext db, IWebHostEnvironment env)
    {
        _db = db;
        _env = env;
    }

    /// <summary>
    /// List all brands with search and pagination
    /// </summary>
    public async Task<IActionResult> Index(string q = "", int page = 1)
    {
        var pageSize = 20;
        var query = _db.Brands.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(b => b.Name.ToLower().Contains(term));
            ViewBag.SearchQuery = q;
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var brands = await query
            .OrderBy(b => b.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Get product count per brand
        var brandIds = brands.Select(b => b.Id).ToList();
        var productCounts = await _db.Products
            .Where(p => p.BrandId.HasValue && brandIds.Contains(p.BrandId.Value))
            .GroupBy(p => p.BrandId)
            .Select(g => new { BrandId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.BrandId ?? 0, x => x.Count);

        ViewBag.ProductCounts = productCounts;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalBrands = totalItems;

        return View(brands);
    }

    /// <summary>
    /// Show create brand form
    /// </summary>
    [HttpGet]
    public IActionResult Create()
    {
        return View(new Brand());
    }

    /// <summary>
    /// Create a new brand
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Brand brand, IFormFile? logoFile)
    {
        if (!ModelState.IsValid)
        {
            return View(brand);
        }

        // Check if brand name already exists
        if (await _db.Brands.AnyAsync(b => b.Name.ToLower() == brand.Name.ToLower()))
        {
            ModelState.AddModelError("Name", "A brand with this name already exists.");
            return View(brand);
        }

        // Handle logo upload
        if (logoFile != null && logoFile.Length > 0)
        {
            brand.Logo = await UploadLogo(logoFile);
        }

        _db.Brands.Add(brand);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Brand '{brand.Name}' created successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Show edit brand form
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var brand = await _db.Brands.FindAsync(id);
        if (brand == null)
        {
            return NotFound();
        }

        ViewBag.ProductCount = await _db.Products.CountAsync(p => p.BrandId == id);
        return View(brand);
    }

    /// <summary>
    /// Update an existing brand
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Brand brand, IFormFile? logoFile)
    {
        if (id != brand.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(brand);
        }

        // Check if another brand with same name exists
        if (await _db.Brands.AnyAsync(b => b.Name.ToLower() == brand.Name.ToLower() && b.Id != id))
        {
            ModelState.AddModelError("Name", "A brand with this name already exists.");
            return View(brand);
        }

        var existingBrand = await _db.Brands.FindAsync(id);
        if (existingBrand == null)
        {
            return NotFound();
        }

        existingBrand.Name = brand.Name;

        // Handle logo upload
        if (logoFile != null && logoFile.Length > 0)
        {
            // Delete old logo if exists
            if (!string.IsNullOrEmpty(existingBrand.Logo))
            {
                DeleteLogo(existingBrand.Logo);
            }
            existingBrand.Logo = await UploadLogo(logoFile);
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"Brand '{brand.Name}' updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Delete a brand
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var brand = await _db.Brands.FindAsync(id);
        if (brand == null)
        {
            return NotFound();
        }

        // Check if brand has products
        var productCount = await _db.Products.CountAsync(p => p.BrandId == id);
        if (productCount > 0)
        {
            TempData["Error"] = $"Cannot delete brand '{brand.Name}' because it has {productCount} products. Please reassign products first.";
            return RedirectToAction(nameof(Index));
        }

        // Delete logo
        if (!string.IsNullOrEmpty(brand.Logo))
        {
            DeleteLogo(brand.Logo);
        }

        _db.Brands.Remove(brand);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"Brand '{brand.Name}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Remove brand logo
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLogo(int id)
    {
        var brand = await _db.Brands.FindAsync(id);
        if (brand == null)
        {
            return NotFound();
        }

        if (!string.IsNullOrEmpty(brand.Logo))
        {
            DeleteLogo(brand.Logo);
            brand.Logo = null;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Logo removed successfully.";
        }

        return RedirectToAction(nameof(Edit), new { id });
    }

    // Helper Methods
    private async Task<string> UploadLogo(IFormFile file)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "brands");
        if (!Directory.Exists(uploadsDir))
        {
            Directory.CreateDirectory(uploadsDir);
        }

        var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
        var filePath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/brands/{fileName}";
    }

    private void DeleteLogo(string logoPath)
    {
        if (string.IsNullOrEmpty(logoPath)) return;

        var fullPath = Path.Combine(_env.WebRootPath, logoPath.TrimStart('/'));
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}
