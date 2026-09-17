using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Marketing;

namespace Sparkle.Api.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MarketingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MarketingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var campaigns = await _context.Campaigns
                .Include(c => c.Products)
                .OrderByDescending(c => c.StartDate)
                .ToListAsync();
            return View(campaigns);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Campaign campaign)
        {
            if (ModelState.IsValid)
            {
                // Basic slug generation
                campaign.Slug = campaign.Name.ToLower().Replace(" ", "-");
                campaign.CreatedAt = DateTime.UtcNow;
                campaign.UpdatedAt = DateTime.UtcNow;

                _context.Add(campaign);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Campaign created successfully!";
                return RedirectToAction(nameof(Index));
            }
            return View(campaign);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var campaign = await _context.Campaigns
                .Include(c => c.Products).ThenInclude(cp => cp.Product)
                .Include(c => c.Categories).ThenInclude(cc => cc.Category)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (campaign == null) return NotFound();
            return View(campaign);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Campaign campaign)
        {
            if (id != campaign.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _context.Campaigns.FindAsync(id);
                    if (existing == null) return NotFound();

                    existing.Name = campaign.Name;
                    existing.Description = campaign.Description;
                    existing.CampaignType = campaign.CampaignType;
                    existing.StartDate = campaign.StartDate;
                    existing.EndDate = campaign.EndDate;
                    existing.IsActive = campaign.IsActive;
                    existing.IsFeatured = campaign.IsFeatured;
                    existing.DisplayOrder = campaign.DisplayOrder;
                    existing.BackgroundColor = campaign.BackgroundColor;
                    existing.TextColor = campaign.TextColor;
                    existing.UpdatedAt = DateTime.UtcNow;
                    
                    // Update slug if name changed? keeping simple for now.

                    _context.Update(existing);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Campaign updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CampaignExists(campaign.Id)) return NotFound();
                    else throw;
                }
            }
            return View(campaign);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var campaign = await _context.Campaigns.FindAsync(id);
            if (campaign == null) return NotFound();

            campaign.IsActive = !campaign.IsActive;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var campaign = await _context.Campaigns.FindAsync(id);
            if (campaign == null) return NotFound();

            _context.Campaigns.Remove(campaign);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Campaign deleted.";
            return RedirectToAction(nameof(Index));
        }
        
        // Product Management
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddProduct(int campaignId, int productId, decimal? specialPrice, int displayOrder)
        {
            var campaign = await _context.Campaigns.FindAsync(campaignId);
            if (campaign == null) return NotFound();

            var product = await _context.Products.FindAsync(productId);
            if (product == null)
            {
                TempData["Error"] = "Product not found.";
                return RedirectToAction(nameof(Edit), new { id = campaignId });
            }

            var exists = await _context.CampaignProducts.AnyAsync(cp => cp.CampaignId == campaignId && cp.ProductId == productId);
            if (exists)
            {
                TempData["Error"] = "Product is already in this campaign.";
                return RedirectToAction(nameof(Edit), new { id = campaignId });
            }

            var campaignProduct = new CampaignProduct
            {
                CampaignId = campaignId,
                ProductId = productId,
                SpecialPrice = specialPrice,
                DisplayOrder = displayOrder,
                CreatedAt = DateTime.UtcNow
            };

            _context.CampaignProducts.Add(campaignProduct);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = campaignId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveProduct(int id)
        {
            var item = await _context.CampaignProducts.FindAsync(id);
            if (item == null) return NotFound();

            var campaignId = item.CampaignId;
            _context.CampaignProducts.Remove(item);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = campaignId });
        }

        // Category Management
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddCategory(int campaignId, int categoryId, decimal? discountPercentage, int displayOrder)
        {
            var campaign = await _context.Campaigns.FindAsync(campaignId);
            if (campaign == null) return NotFound();

            var exists = await _context.CampaignCategories.AnyAsync(cc => cc.CampaignId == campaignId && cc.CategoryId == categoryId);
            if (exists)
            {
                TempData["Error"] = "Category is already in this campaign.";
                return RedirectToAction(nameof(Edit), new { id = campaignId });
            }

            var campaignCategory = new CampaignCategory
            {
                CampaignId = campaignId,
                CategoryId = categoryId,
                DiscountPercentage = discountPercentage,
                DisplayOrder = displayOrder,
                CreatedAt = DateTime.UtcNow
            };

            _context.CampaignCategories.Add(campaignCategory);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = campaignId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveCategory(int id)
        {
            var item = await _context.CampaignCategories.FindAsync(id);
            if (item == null) return NotFound();

            var campaignId = item.CampaignId;
            _context.CampaignCategories.Remove(item);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Edit), new { id = campaignId });
        }

        private bool CampaignExists(int id)
        {
            return _context.Campaigns.Any(e => e.Id == id);
        }
    }
}
