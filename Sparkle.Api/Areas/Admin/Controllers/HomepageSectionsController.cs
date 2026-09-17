using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Content;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class HomepageSectionsController : Controller
{
    private readonly ApplicationDbContext _db;

    public HomepageSectionsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET: /Admin/HomepageSections
    public async Task<IActionResult> Index()
    {
        var sections = await _db.HomepageSections
            .Include(s => s.ManualProducts)
            .ThenInclude(sp => sp.Product)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync();

        return View(sections);
    }

    // GET: /Admin/HomepageSections/Create
    public IActionResult Create()
    {
        return View(new HomepageSection());
    }

    // POST: /Admin/HomepageSections/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(HomepageSection section)
    {
        if (ModelState.IsValid)
        {
            section.CreatedAt = DateTime.UtcNow;
            section.Slug = (section.Name ?? string.Empty).ToLower().Replace(" ", "-");
            _db.HomepageSections.Add(section);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Section created successfully!";
            return RedirectToAction(nameof(Index));
        }

        return View(section);
    }

    // GET: /Admin/HomepageSections/Edit/5
    public async Task<IActionResult> Edit(int id)
    {
        var section = await _db.HomepageSections
            .Include(s => s.ManualProducts)
            .ThenInclude(sp => sp.Product)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (section == null)
            return NotFound();

        return View(section);
    }

    // POST: /Admin/HomepageSections/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, HomepageSection section)
    {
        if (id != section.Id)
            return NotFound();

        if (ModelState.IsValid)
        {
            var existingSection = await _db.HomepageSections.FindAsync(id);
            if (existingSection == null)
            {
                return NotFound();
            }

            // Update allowed properties
            existingSection.Name = section.Name;
            existingSection.DisplayTitle = section.DisplayTitle;
            existingSection.SectionType = section.SectionType;
            existingSection.DisplayOrder = section.DisplayOrder;
            existingSection.MaxProductsToDisplay = section.MaxProductsToDisplay;
            existingSection.CardSize = section.CardSize;
            existingSection.LayoutType = section.LayoutType;
            existingSection.BackgroundColor = section.BackgroundColor;
            existingSection.ProductsPerRow = section.ProductsPerRow;
            existingSection.UseAutomatedSelection = section.UseAutomatedSelection;
            existingSection.ShowRating = section.ShowRating;
            existingSection.ShowPrice = section.ShowPrice;
            existingSection.ShowDiscount = section.ShowDiscount;
            existingSection.IsActive = section.IsActive;
            
            // Regenerate slug if needed (and ensure it's not empty)
            if (!string.Equals(existingSection.Name, section.Name, StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(existingSection.Slug))
            {
                existingSection.Slug = (section.Name ?? string.Empty).ToLower().Replace(" ", "-");
            }

            existingSection.UpdatedAt = DateTime.UtcNow;
            
            _db.Update(existingSection);
            await _db.SaveChangesAsync();

            TempData["Success"] = "Section updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        return View(section);
    }

    // POST: /Admin/HomepageSections/Delete/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var section = await _db.HomepageSections.FindAsync(id);
        if (section != null)
        {
            _db.HomepageSections.Remove(section);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Section deleted successfully!";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: /Admin/HomepageSections/ToggleActive/5
    [HttpPost]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var section = await _db.HomepageSections.FindAsync(id);
        if (section == null)
            return Json(new { success = false });

        section.IsActive = !section.IsActive;
        section.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Json(new { success = true, isActive = section.IsActive });
    }

    // GET: /Admin/HomepageSections/ManageProducts/5
    public async Task<IActionResult> ManageProducts(int id)
    {
        var section = await _db.HomepageSections
            .Include(s => s.ManualProducts)
            .ThenInclude(sp => sp.Product)
            .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (section == null)
            return NotFound();

        // Get available products not in this section
        var existingProductIds = section.ManualProducts.Select(sp => sp.ProductId).ToList();
        var availableProducts = await _db.Products
            .Include(p => p.Images)
            .Where(p => !existingProductIds.Contains(p.Id))
            .OrderByDescending(p => p.CreatedAt)
            .Take(50)
            .ToListAsync();

        ViewBag.AvailableProducts = availableProducts;
        return View(section);
    }

    // POST: /Admin/HomepageSections/AddProduct
    [HttpPost]
    public async Task<IActionResult> AddProduct(int sectionId, int productId)
    {
        var exists = await _db.HomepageSectionProducts
            .AnyAsync(sp => sp.SectionId == sectionId && sp.ProductId == productId);

        if (exists)
            return Json(new { success = false, message = "Product already in section" });

        var maxOrder = await _db.HomepageSectionProducts
            .Where(sp => sp.SectionId == sectionId)
            .MaxAsync(sp => (int?)sp.DisplayOrder) ?? 0;

        var sectionProduct = new HomepageSectionProduct
        {
            SectionId = sectionId,
            ProductId = productId,
            DisplayOrder = maxOrder + 1
        };

        _db.HomepageSectionProducts.Add(sectionProduct);
        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }

    // POST: /Admin/HomepageSections/RemoveProduct
    [HttpPost]
    public async Task<IActionResult> RemoveProduct(int id)
    {
        var sectionProduct = await _db.HomepageSectionProducts.FindAsync(id);
        if (sectionProduct == null)
            return Json(new { success = false });

        _db.HomepageSectionProducts.Remove(sectionProduct);
        await _db.SaveChangesAsync();

        return Json(new { success = true });
    }

    // POST: /Admin/HomepageSections/ReorderProducts
    [HttpPost]
    public async Task<IActionResult> ReorderProducts(int sectionId, List<int> productIds)
    {
        var sectionProducts = await _db.HomepageSectionProducts
            .Where(sp => sp.SectionId == sectionId)
            .ToListAsync();

        for (int i = 0; i < productIds.Count; i++)
        {
            var sp = sectionProducts.FirstOrDefault(x => x.ProductId == productIds[i]);
            if (sp != null)
            {
                sp.DisplayOrder = i;
            }
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }

    // POST: /Admin/HomepageSections/UpdateOrder
    [HttpPost]
    public async Task<IActionResult> UpdateOrder([FromBody] List<int> sectionIds)
    {
        var sections = await _db.HomepageSections.ToListAsync();
        
        for (int i = 0; i < sectionIds.Count; i++)
        {
            var section = sections.FirstOrDefault(s => s.Id == sectionIds[i]);
            if (section != null)
            {
                section.DisplayOrder = i;
            }
        }

        await _db.SaveChangesAsync();
        return Json(new { success = true });
    }
}
