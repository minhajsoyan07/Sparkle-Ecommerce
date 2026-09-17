using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Catalog;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ProductsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ProductsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string status = "all", string q = "", string sort = "date_desc", int page = 1)
    {
        var query = _db.Products
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .Include(p => p.Images)
            .AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(p => 
                p.Title.ToLower().Contains(term) || 
                (p.Description != null && p.Description.ToLower().Contains(term)) ||
                (p.Seller != null && p.Seller.ShopName.ToLower().Contains(term)));
        }

        // Filter
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (status == "approved") query = query.Where(p => p.ModerationStatus == ProductModerationStatus.Approved || (p.IsAdminProduct && p.IsActive));
            else if (status == "draft") query = query.Where(p => !p.IsActive && p.ModerationStatus == ProductModerationStatus.Draft);
            else if (status == "pending") query = query.Where(p => !p.IsAdminProduct && p.ModerationStatus == ProductModerationStatus.Pending);
            else if (status == "rejected") query = query.Where(p => !p.IsAdminProduct && p.ModerationStatus == ProductModerationStatus.Rejected);
            else if (status == "suspended") query = query.Where(p => !p.IsAdminProduct && p.ModerationStatus == ProductModerationStatus.Suspended);
        }

        // Sorting
        query = sort switch
        {
            "name_asc" => query.OrderBy(p => p.Title),
            "name_desc" => query.OrderByDescending(p => p.Title),
            "id_asc" => query.OrderBy(p => p.Id),
            "id_desc" => query.OrderByDescending(p => p.Id),
            "category_asc" => query.OrderBy(p => p.Category!.Name),
            "category_desc" => query.OrderByDescending(p => p.Category!.Name),
            "date_asc" => query.OrderBy(p => p.CreatedAt),
            "date_desc" => query.OrderByDescending(p => p.CreatedAt), // Default
            "shop_asc" => query.OrderBy(p => p.Seller!.ShopName),
            "shop_desc" => query.OrderByDescending(p => p.Seller!.ShopName),
            _ => query.OrderByDescending(p => p.CreatedAt)
        };

        var totalItems = await query.CountAsync();
        var pageSize = 20;
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.CurrentStatus = status;
        ViewBag.SearchQuery = q;
        ViewBag.CurrentSort = sort;
        ViewBag.TotalItems = totalItems; // For debugging
        
        // Calculate badge counts for status filters
        ViewBag.PendingCount = await _db.Products.CountAsync(p => !p.IsAdminProduct && p.ModerationStatus == ProductModerationStatus.Pending);
        ViewBag.DraftCount = await _db.Products.CountAsync(p => !p.IsActive && p.ModerationStatus == ProductModerationStatus.Draft);
        ViewBag.ApprovedCount = await _db.Products.CountAsync(p => p.ModerationStatus == ProductModerationStatus.Approved || (p.IsAdminProduct && p.IsActive));
        ViewBag.RejectedCount = await _db.Products.CountAsync(p => !p.IsAdminProduct && p.ModerationStatus == ProductModerationStatus.Rejected);
        ViewBag.SuspendedCount = await _db.Products.CountAsync(p => !p.IsAdminProduct && p.ModerationStatus == ProductModerationStatus.Suspended);

        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Category)
            .Include(p => p.Seller)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        if (product.Seller != null && !string.IsNullOrEmpty(product.Seller.UserId))
        {
            // Fetch associated user for fallback contact info
            ViewBag.SellerUser = await _db.Users.FindAsync(product.Seller.UserId);
        }

        return View(product);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product)
    {
        if (ModelState.IsValid)
        {
            product.CreatedAt = DateTime.UtcNow;
            product.IsAdminProduct = true; // Admin products are official
            product.SellerId = null; // Platform product

            _db.Products.Add(product);
            await _db.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        ViewBag.Categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(product);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var product = await _db.Products
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }
        
        // Only allow editing admin-created products
        if (!product.IsAdminProduct)
        {
            TempData["Error"] = "Cannot edit seller products. Sellers must edit their own products.";
            return RedirectToAction(nameof(Details), new { id });
        }
        
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product)
    {
        if (id != product.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                var existing = await _db.Products.FindAsync(id);
                if (existing == null) return NotFound();
                
                // Only allow editing admin-created products
                if (!existing.IsAdminProduct)
                {
                    TempData["Error"] = "Cannot edit seller products. Sellers must edit their own products.";
                    return RedirectToAction(nameof(Details), new { id });
                }

                // Only update allowed fields for admin
                existing.Title = product.Title;
                existing.BasePrice = product.BasePrice;
                existing.IsActive = product.IsActive;
                existing.IsAdminProduct = product.IsAdminProduct;
                
                _db.Update(existing);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(product.Id))
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
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product != null)
        {
            // Only allow deleting admin-created products
            if (!product.IsAdminProduct)
            {
                TempData["Error"] = "Cannot delete seller products. Use moderation actions instead.";
                return RedirectToAction(nameof(Details), new { id });
            }
            
            _db.Products.Remove(product);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Product deleted successfully.";
        }
        return RedirectToAction(nameof(Index));
    }

    private bool ProductExists(int id)
    {
        return _db.Products.Any(e => e.Id == id);
    }
    
    #region Moderation Queue
    
    /// <summary>
    /// Product moderation queue - shows products pending admin approval
    /// </summary>

    
    /// <summary>
    /// Approve a product for listing
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? notes = null, string? returnUrl = null)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();
        
        product.ModerationStatus = ProductModerationStatus.Approved;
        product.ModerationNotes = notes;
        product.ModeratedAt = DateTime.UtcNow;
        product.ModeratedBy = User.Identity?.Name;
        product.IsActive = true; // Activate immediately upon approval
        
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"Product '{product.Title}' approved successfully.";
        
        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }

        // Redirect back to pending list - approved product will automatically be excluded
        return RedirectToAction(nameof(Index), new { status = "pending" });
    }
    
    /// <summary>
    /// Reject a product with reason
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason, string? returnUrl = null)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();
        
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Rejection reason is required.";
            if (!string.IsNullOrEmpty(returnUrl)) return Redirect(returnUrl);
            return RedirectToAction(nameof(Index), new { status = "pending" });
        }
        
        product.ModerationStatus = ProductModerationStatus.Rejected;
        product.ModerationNotes = reason;
        product.ModeratedAt = DateTime.UtcNow;
        product.ModeratedBy = User.Identity?.Name;
        product.IsActive = false;
        
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"Product '{product.Title}' rejected.";
        
        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index), new { status = "pending" });
    }
    
    /// <summary>
    /// Suspend an already approved product
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string reason, string? returnUrl = null)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return NotFound();
        
        product.ModerationStatus = ProductModerationStatus.Suspended;
        product.ModerationNotes = reason;
        product.ModeratedAt = DateTime.UtcNow;
        product.ModeratedBy = User.Identity?.Name;
        product.IsActive = false;
        
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"Product '{product.Title}' suspended.";
        
        if (!string.IsNullOrEmpty(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Details), new { id });
    }
    
    /// <summary>
    /// Quick approve via AJAX for faster moderation
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> QuickApprove(int id)
    {
        var product = await _db.Products.FindAsync(id);
        if (product == null) return Json(new { success = false, message = "Product not found" });
        
        product.ModerationStatus = ProductModerationStatus.Approved;
        product.ModeratedAt = DateTime.UtcNow;
        product.ModeratedBy = User.Identity?.Name;
        product.IsActive = true;
        
        await _db.SaveChangesAsync();
        
        return Json(new { success = true, message = "Product approved" });
    }
    
    /// <summary>
    /// Bulk approve multiple products
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkApprove(int[] productIds)
    {
        if (productIds == null || productIds.Length == 0)
        {
            TempData["Error"] = "No products selected.";
            return RedirectToAction(nameof(Index), new { status = "pending" });
        }

        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        
        foreach (var product in products)
        {
            product.ModerationStatus = ProductModerationStatus.Approved;
            product.ModeratedAt = DateTime.UtcNow;
            product.ModeratedBy = User.Identity?.Name;
            product.IsActive = true;
        }
        
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"{products.Count} product(s) approved successfully.";
        return RedirectToAction(nameof(Index), new { status = "pending" });
    }
    
    /// <summary>
    /// Bulk reject multiple products
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BulkReject(int[] productIds, string reason)
    {
        if (productIds == null || productIds.Length == 0)
        {
            TempData["Error"] = "No products selected.";
            return RedirectToAction(nameof(Index), new { status = "pending" });
        }
        
        if (string.IsNullOrWhiteSpace(reason))
        {
            TempData["Error"] = "Rejection reason is required.";
            return RedirectToAction(nameof(Index), new { status = "pending" });
        }

        var products = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync();
        
        foreach (var product in products)
        {
            product.ModerationStatus = ProductModerationStatus.Rejected;
            product.ModerationNotes = reason;
            product.ModeratedAt = DateTime.UtcNow;
            product.ModeratedBy = User.Identity?.Name;
            product.IsActive = false;
        }
        
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"{products.Count} product(s) rejected.";
        return RedirectToAction(nameof(Index), new { status = "pending" });
    }
    
    #endregion
}

