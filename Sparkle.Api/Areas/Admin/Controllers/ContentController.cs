using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Content;


namespace Sparkle.Api.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ContentController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ContentController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var pages = await _context.StaticPages
                .OrderBy(p => p.DisplayOrder)
                .ToListAsync();
            return View(pages);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(StaticPage model)
        {
            if (ModelState.IsValid)
            {
                // Auto-generate slug if empty
                if (string.IsNullOrWhiteSpace(model.Slug))
                {
                    model.Slug = model.Title.ToLower().Replace(" ", "-").Replace(".", "").Replace("/", "");
                }
                
                // Ensure slug uniqueness could be added here

                model.CreatedAt = DateTime.UtcNow;
                model.UpdatedAt = DateTime.UtcNow;

                _context.Add(model);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Page created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var page = await _context.StaticPages.FindAsync(id);
            if (page == null) return NotFound();
            return View(page);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, StaticPage model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.StaticPages.FindAsync(id);
                    if (existing == null) return NotFound();

                    existing.Title = model.Title;
                    existing.Slug = model.Slug; // Allow editing slug
                    existing.Content = model.Content;
                    existing.MetaTitle = model.MetaTitle;
                    existing.MetaDescription = model.MetaDescription;
                    existing.IsPublished = model.IsPublished;
                    existing.DisplayOrder = model.DisplayOrder;
                    existing.Location = model.Location;
                    existing.UpdatedAt = DateTime.UtcNow;

                    _context.Update(existing);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Page updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PageExists(model.Id)) return NotFound();
                    else throw;
                }
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var page = await _context.StaticPages.FindAsync(id);
            if (page == null) return NotFound();

            _context.StaticPages.Remove(page);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Page deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var page = await _context.StaticPages.FindAsync(id);
            if (page == null) return NotFound();

            page.IsPublished = !page.IsPublished;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PageExists(int id)
        {
            return _context.StaticPages.Any(e => e.Id == id);
        }
    }
}
