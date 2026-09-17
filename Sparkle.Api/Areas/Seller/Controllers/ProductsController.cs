using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Catalog;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Seller.Controllers;

[Area("Seller")]
[Authorize(Roles = "Seller")]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IWebHostEnvironment _webHostEnvironment;

    public ProductsController(ApplicationDbContext db, IWebHostEnvironment webHostEnvironment)
    {
        _db = db;
        _webHostEnvironment = webHostEnvironment;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException();

    public async Task<IActionResult> Index(string? q = null, string status = "all", string sort = "newest", int page = 1)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return RedirectToAction("Register", "Seller", new { area = "" });

        int pageSize = 20;
        var query = _db.Products
            .Include(p => p.Category)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Where(p => p.SellerId == seller.Id);

        // Search filter (server-side)
        if (!string.IsNullOrWhiteSpace(q))
        {
            var searchTerm = q.Trim().ToLower();
            query = query.Where(p => 
                p.Title.ToLower().Contains(searchTerm) || 
                p.Id.ToString().Contains(searchTerm) ||
                (p.Slug != null && p.Slug.ToLower().Contains(searchTerm)));
        }

        // Status filter (server-side)
        if (status == "active")
        {
            query = query.Where(p => p.IsActive);
        }
        else if (status == "inactive")
        {
            query = query.Where(p => !p.IsActive);
        }

        // Count totals AFTER status filter for accurate counts
        int totalItems = await query.CountAsync();
        int activeCount = status == "all" ? await query.CountAsync(p => p.IsActive) : (status == "active" ? totalItems : 0);
        int inactiveCount = status == "all" ? await query.CountAsync(p => !p.IsActive) : (status == "inactive" ? totalItems : 0);

        // Apply sorting (server-side)
        query = sort switch
        {
            "name-asc" => query.OrderBy(p => p.Title),
            "name-desc" => query.OrderByDescending(p => p.Title),
            "oldest" => query.OrderBy(p => p.CreatedAt),
            "stock-high" => query.OrderByDescending(p => p.Variants.Sum(v => v.Stock)),
            "stock-low" => query.OrderBy(p => p.Variants.Sum(v => v.Stock)),
            "price-high" => query.OrderByDescending(p => p.BasePrice),
            "price-low" => query.OrderBy(p => p.BasePrice),
            _ => query.OrderByDescending(p => p.CreatedAt) // newest (default)
        };

        // Pagination
        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Pass filter state to view
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        ViewBag.TotalProducts = totalItems;
        ViewBag.ActiveCount = activeCount;
        ViewBag.InactiveCount = inactiveCount;
        ViewBag.CurrentSearch = q ?? "";
        ViewBag.CurrentStatus = status;
        ViewBag.CurrentSort = sort;

        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = new SelectList(await _db.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name");
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product, List<IFormFile> images, int initialStock = 0)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return RedirectToAction("Register", "Seller", new { area = "" });

        if (ModelState.IsValid)
        {
            product.SellerId = seller.Id;
            product.CreatedAt = DateTime.UtcNow;
            product.IsActive = false; // Products start inactive until admin approves
            product.ModerationStatus = ProductModerationStatus.Pending; // Submit for admin review
            
            if (string.IsNullOrEmpty(product.Slug))
            {
                product.Slug = product.Title.ToLower().Replace(" ", "-") + "-" + Guid.NewGuid().ToString().Substring(0, 6);
            }

            _db.Products.Add(product);
            await _db.SaveChangesAsync();

            // Create a default variant with the initial stock
            var defaultVariant = new ProductVariant
            {
                ProductId = product.Id,
                Sku = $"SKU-{product.Id}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
                Price = product.BasePrice,
                Stock = Math.Max(0, initialStock),
                Color = "Default",
                Size = "Standard"
            };
            _db.ProductVariants.Add(defaultVariant);
            await _db.SaveChangesAsync();

            // Handle Images
            if (images != null && images.Any())
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
                Directory.CreateDirectory(uploadsFolder);

                int sortOrder = 1;
                foreach (var img in images)
                {
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + img.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await img.CopyToAsync(fileStream);
                    }

                    var productImage = new ProductImage
                    {
                        ProductId = product.Id,
                        ImagePath = "/uploads/products/" + uniqueFileName,
                        SortOrder = sortOrder++
                    };
                    _db.ProductImages.Add(productImage);
                }
                await _db.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        ViewBag.Categories = new SelectList(await _db.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name");
        return View(product);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return RedirectToAction("Register", "Seller", new { area = "" });

        var product = await _db.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.SellerId == seller.Id);

        if (product == null) return NotFound();

        ViewBag.Categories = new SelectList(await _db.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", product.CategoryId);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product, List<IFormFile> newImages, int? stockUpdate = null)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return RedirectToAction("Register", "Seller", new { area = "" });

        if (id != product.Id) return NotFound();

        if (ModelState.IsValid)
        {
            try
            {
                var existing = await _db.Products.Include(p => p.Images).FirstOrDefaultAsync(p => p.Id == id && p.SellerId == seller.Id);
                if (existing == null) return NotFound();

                existing.Title = product.Title;
                existing.ShortDescription = product.ShortDescription;
                existing.Description = product.Description;
                existing.BasePrice = product.BasePrice;
                existing.DiscountPercent = product.DiscountPercent;
                existing.CategoryId = product.CategoryId;

                // Handle Stock for simple products (single variant)
                if (stockUpdate.HasValue)
                {
                    var variants = await _db.ProductVariants.Where(v => v.ProductId == id).ToListAsync();
                    if (variants.Count == 1)
                    {
                        variants[0].Stock = Math.Max(0, stockUpdate.Value);
                        variants[0].Price = product.BasePrice;
                    }
                }

                // Handle New Images
                if (newImages != null && newImages.Any())
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "products");
                    Directory.CreateDirectory(uploadsFolder);

                    // Shift existing images down to make room for new images
                    if (existing.Images != null && existing.Images.Any())
                    {
                        foreach (var existingImg in existing.Images)
                        {
                            existingImg.SortOrder += newImages.Count;
                        }
                    }

                    int sortOrder = 1;
                    foreach (var img in newImages)
                    {
                        string uniqueFileName = Guid.NewGuid().ToString() + "_" + img.FileName;
                        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                        using (var fileStream = new FileStream(filePath, FileMode.Create))
                        {
                            await img.CopyToAsync(fileStream);
                        }

                        var productImage = new ProductImage
                        {
                            ProductId = existing.Id,
                            ImagePath = "/uploads/products/" + uniqueFileName,
                            SortOrder = sortOrder++
                        };
                        _db.ProductImages.Add(productImage);
                    }
                }

                _db.Update(existing);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_db.Products.Any(p => p.Id == id && p.SellerId == seller.Id))
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
        ViewBag.Categories = new SelectList(await _db.Categories.OrderBy(c => c.Name).ToListAsync(), "Id", "Name", product.CategoryId);
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller == null) return RedirectToAction("Register", "Seller", new { area = "" });

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.SellerId == seller.Id);
        if (product != null)
        {
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }
}
