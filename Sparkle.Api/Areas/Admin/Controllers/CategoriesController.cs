using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Catalog;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly Microsoft.Extensions.Caching.Distributed.IDistributedCache _cache;

    public CategoriesController(ApplicationDbContext db, Microsoft.Extensions.Caching.Distributed.IDistributedCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IActionResult> Index(string q = "")
    {
        var query = _db.Categories.AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(c => c.Name.ToLower().Contains(term));
            ViewBag.SearchQuery = q;
        }

        var categories = await query
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync();

        return View(categories);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var category = await _db.Categories
            .Include(c => c.Parent)
            .Include(c => c.SubCategories)
            .FirstOrDefaultAsync(c => c.Id == id);
        
        if (category == null)
        {
            return NotFound();
        }

        // Add product count
        ViewBag.ProductCount = await _db.Products.CountAsync(p => p.CategoryId == id);

        return View(category);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category, IFormFile? ImageFile)
    {
        if (ModelState.IsValid)
        {
            if (string.IsNullOrEmpty(category.Slug))
            {
                category.Slug = category.Name.ToLower().Replace(" ", "-");
            }

            // Handle image upload
            if (ImageFile != null && ImageFile.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "categories");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = Guid.NewGuid().ToString() + "_" + ImageFile.FileName;
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await ImageFile.CopyToAsync(fileStream);
                }

                category.ImageUrl = "/uploads/categories/" + uniqueFileName;
            }

            _db.Categories.Add(category);
            await _db.SaveChangesAsync();
            
            await _cache.RemoveAsync("homepage_data_v3");
            
            TempData["Success"] = "Category created successfully!";
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var category = await _db.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category, IFormFile? ImageFile, bool RemoveImage = false)
    {
        if (id != category.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                if (string.IsNullOrEmpty(category.Slug))
                {
                    category.Slug = category.Name.ToLower().Replace(" ", "-");
                }
                
                // Force ImageUrl to null if the admin explicitly requested removal
                if (RemoveImage)
                {
                    category.ImageUrl = null;
                }
                
                var existingCategory = await _db.Categories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
                
                // If admin removed the image, delete it from disk
                if (existingCategory != null && !string.IsNullOrEmpty(existingCategory.ImageUrl) && string.IsNullOrEmpty(category.ImageUrl))
                {
                    var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingCategory.ImageUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Handle new image upload
                if (ImageFile != null && ImageFile.Length > 0)
                {
                    // Delete old image if it wasn't already handled above
                    if (existingCategory != null && !string.IsNullOrEmpty(existingCategory.ImageUrl) && !string.IsNullOrEmpty(category.ImageUrl))
                    {
                        var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", existingCategory.ImageUrl.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    // Save new image
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "categories");
                    Directory.CreateDirectory(uploadsFolder);

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + ImageFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageFile.CopyToAsync(fileStream);
                    }

                    category.ImageUrl = "/uploads/categories/" + uniqueFileName;
                }
                
                _db.Update(category);
                await _db.SaveChangesAsync();
                
                // Clear homepage cache to immediately reflect category changes
                await _cache.RemoveAsync("homepage_data_v3");
                
                TempData["Success"] = "Category updated successfully!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!CategoryExists(category.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.FindAsync(id);
        if (category != null)
        {
            _db.Categories.Remove(category);
            await _db.SaveChangesAsync();
            await _cache.RemoveAsync("homepage_data_v3");
        }
        return RedirectToAction(nameof(Index));
    }

    private bool CategoryExists(int id)
    {
        return _db.Categories.Any(e => e.Id == id);
    }
}
