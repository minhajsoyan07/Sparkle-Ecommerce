using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Marketing;
using Sparkle.Domain.Catalog;

namespace Sparkle.Api.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class FlashSalesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public FlashSalesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var deals = await _context.FlashDeals
                .Include(f => f.Products)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();
            return View(deals);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(FlashDeal flashDeal, IFormFile? bannerImage)
        {
            if (ModelState.IsValid)
            {
                // Handle image upload if needed (omitted for brevity, assuming URL or handled elsewhere for now)
                // For this implementation, we'll assume BannerImage property is a string URL or filename
                // In a real app, we'd upload to cloud/disk.
                
                // Set defaults
                flashDeal.CreatedAt = DateTime.UtcNow;
                flashDeal.UpdatedAt = DateTime.UtcNow;

                _context.Add(flashDeal);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Flash deal created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(flashDeal);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var flashDeal = await _context.FlashDeals
                .Include(f => f.Products)
                .ThenInclude(fp => fp.Product)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (flashDeal == null) return NotFound();
            return View(flashDeal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, FlashDeal flashDeal)
        {
            if (id != flashDeal.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.FlashDeals.FindAsync(id);
                    if (existing == null) return NotFound();

                    existing.Name = flashDeal.Name;
                    existing.Description = flashDeal.Description;
                    existing.StartTime = flashDeal.StartTime;
                    existing.EndTime = flashDeal.EndTime;
                    existing.IsActive = flashDeal.IsActive;
                    existing.ShowOnHomepage = flashDeal.ShowOnHomepage;
                    existing.BadgeText = flashDeal.BadgeText;
                    existing.DisplayOrder = flashDeal.DisplayOrder;
                    existing.UpdatedAt = DateTime.UtcNow;

                    _context.Update(existing);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Flash deal updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FlashDealExists(flashDeal.Id)) return NotFound();
                    else throw;
                }
            }
            return View(flashDeal);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var deal = await _context.FlashDeals.FindAsync(id);
            if (deal == null) return NotFound();

            deal.IsActive = !deal.IsActive;
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Flash deal {(deal.IsActive ? "activated" : "deactivated")} successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var deal = await _context.FlashDeals.Include(f => f.Products).FirstOrDefaultAsync(f => f.Id == id);
            if (deal == null) return NotFound();

            if (deal.Products.Any())
            {
                 TempData["Error"] = "Cannot delete flash deal with associated products. Remove products first.";
                 return RedirectToAction(nameof(Index));
            }

            _context.FlashDeals.Remove(deal);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Flash deal deleted successfully.";
            return RedirectToAction(nameof(Index));
        }
        
        // Product Management Actions
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProduct(int flashDealId, int productId, decimal dealPrice, int stockLimit)
        {
            var deal = await _context.FlashDeals.FindAsync(flashDealId);
            if (deal == null) return NotFound();
            
            var product = await _context.Products.FindAsync(productId);
            if (product == null) {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(Edit), new { id = flashDealId });
            }

            var exists = await _context.FlashDealProducts.AnyAsync(fp => fp.FlashDealId == flashDealId && fp.ProductId == productId);
            if (exists) {
                TempData["Error"] = "Product already in this deal.";
                return RedirectToAction(nameof(Edit), new { id = flashDealId });
            }

            var dealProduct = new FlashDealProduct
            {
                FlashDealId = flashDealId,
                ProductId = productId,
                OriginalPrice = product.Price,
                DealPrice = dealPrice,
                DiscountPercentage = product.Price > 0 ? ((product.Price - dealPrice) / product.Price) * 100 : 0,
                StockLimit = stockLimit,
                SoldCount = 0,
                CreatedAt = DateTime.UtcNow
            };

            _context.FlashDealProducts.Add(dealProduct);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Product added to deal.";
            return RedirectToAction(nameof(Edit), new { id = flashDealId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveProduct(int id)
        {
            var dealProduct = await _context.FlashDealProducts.FindAsync(id);
            if (dealProduct == null) return NotFound();

            int dealId = dealProduct.FlashDealId;
            _context.FlashDealProducts.Remove(dealProduct);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Product removed from deal.";
            return RedirectToAction(nameof(Edit), new { id = dealId });
        }

        private bool FlashDealExists(int id)
        {
            return _context.FlashDeals.Any(e => e.Id == id);
        }
    }
}
