using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Sellers;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class SellersController : Controller
{
    private readonly ApplicationDbContext _db;

    public SellersController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string status = "all", string q ="", int page = 1)
    {
        // Calculate counts for tabs
        ViewBag.PendingCount = await _db.Sellers.CountAsync(s => s.Status == SellerStatus.Pending);
        ViewBag.ApprovedCount = await _db.Sellers.CountAsync(s => s.Status == SellerStatus.Approved);
        ViewBag.RejectedCount = await _db.Sellers.CountAsync(s => s.Status == SellerStatus.Rejected);
        ViewBag.IsolatedCount = await _db.Sellers.CountAsync(s => s.Status == SellerStatus.Isolated);

        var query = _db.Sellers.AsQueryable();

        // Search
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(s => (s.ShopName != null && s.ShopName.Contains(q)) || (s.MobileNumber != null && s.MobileNumber.Contains(q)) || (s.Email != null && s.Email.Contains(q)));
        }

        // Filter
        if (!string.IsNullOrWhiteSpace(status) && status != "all")
        {
            if (Enum.TryParse<SellerStatus>(status, true, out var statusEnum))
            {
                query = query.Where(s => s.Status == statusEnum);
            }
        }

        // Pagination
        int pageSize = 20;
        int totalItems = await query.CountAsync();
        int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
        page = Math.Max(1, Math.Min(page, totalPages > 0 ? totalPages : 1));

        var sellers = await query
            .OrderByDescending(v => v.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        if (sellers.Any())
        {
            var sellerIds = sellers.Select(s => s.Id).ToList();

            // Batch fetch product counts
            var productCounts = await _db.Products
                .Where(p => p.SellerId != null && sellerIds.Contains(p.SellerId!.Value))
                .GroupBy(p => p.SellerId)
                .Select(g => new { SellerId = g.Key ?? 0, Count = g.Count() })
                .ToDictionaryAsync(k => k.SellerId, v => v.Count);

            // Batch fetch sales counts (Total distinct orders)
            var salesCounts = await _db.OrderItems
                .Where(oi => oi.SellerId != null && sellerIds.Contains(oi.SellerId.Value))
                .GroupBy(oi => oi.SellerId)
                .Select(g => new { SellerId = g.Key ?? 0, Count = g.Select(x => x.OrderId).Distinct().Count() })
                .ToDictionaryAsync(k => k.SellerId, v => v.Count);

            // Fetch user emails for sellers
            var userIds = sellers.Select(s => s.UserId).Distinct().ToList();
            var userEmails = await _db.Users
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Email);

             foreach (var seller in sellers)
            {
                seller.TotalProducts = productCounts.GetValueOrDefault(seller.Id, 0);
                seller.TotalSales = salesCounts.GetValueOrDefault(seller.Id, 0);
                
                if (string.IsNullOrEmpty(seller.Email) && userEmails.TryGetValue(seller.UserId, out var email))
                {
                    seller.Email = email;
                }
            }
        }

        ViewBag.CurrentStatus = status;
        ViewBag.SearchQuery = q;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;

        return View(sellers);
    }

    public async Task<IActionResult> Details(int id)
    {
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.Id == id);
        if (seller == null)
        {
            return NotFound();
        }

        var products = await _db.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Where(p => p.SellerId == seller.Id)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        ViewBag.Products = products;
        ViewBag.User = await _db.Users.FindAsync(seller.UserId); // Fetch associated user

        return View(seller);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleStatus(int sellerId)
    {
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.Id == sellerId);
        if (seller == null)
        {
            return Json(new { success = false, message = "Seller not found" });
        }

        // Toggle between Approved and Pending (or handle logic as needed)
        // Note: For now mapping Active to Approved.
        if (seller.Status == SellerStatus.Approved)
            seller.Status = SellerStatus.Pending;
        else
            seller.Status = SellerStatus.Approved;

        await _db.SaveChangesAsync();

        return Json(new { success = true, status = seller.Status.ToString(), isActive = seller.Status == SellerStatus.Approved });
    }

    [HttpPost]
    public async Task<IActionResult> Approve(int sellerId)
    {
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.Id == sellerId);
        if (seller == null) return NotFound();

        seller.Status = SellerStatus.Approved;
        seller.ApprovedAt = DateTime.UtcNow;
        // Optionally unblock user login if implemented in Identity
        
        await _db.SaveChangesAsync();

        TempData["Success"] = "Seller approved successfully.";
        return RedirectToAction(nameof(Details), new { id = sellerId });
    }

    [HttpPost]
    public async Task<IActionResult> Decline(int sellerId)
    {
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.Id == sellerId);
        if (seller == null) return NotFound();

        seller.Status = SellerStatus.Rejected;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Seller application declined.";
        return RedirectToAction(nameof(Details), new { id = sellerId });
    }

    [HttpPost]
    public async Task<IActionResult> Isolate(int sellerId)
    {
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.Id == sellerId);
        if (seller == null) return NotFound();

        seller.Status = SellerStatus.Isolated;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Seller isolated.";
        return RedirectToAction(nameof(Details), new { id = sellerId });
    }

    [HttpPost]
    public async Task<IActionResult> Suspend(int sellerId)
    {
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.Id == sellerId);
        if (seller == null) return NotFound();

        // Mapping Suspended to Rejected or Isolated?
        // Let's use Rejected for now as 'Suspended' enum is gone.
        // Or actually, if 'Suspended' was distinct, we might be missing a state.
        // But for this task, 'Decline' is the main negative state.
        // I will map Suspend to Isolated as it implies temporary restriction.
        seller.Status = SellerStatus.Isolated; 
        await _db.SaveChangesAsync();

        TempData["Success"] = "Seller suspended (isolated).";
        return RedirectToAction(nameof(Details), new { id = sellerId });
    }

    [HttpGet]
    public async Task<IActionResult> Repair()
    {
        var sellers = await _db.Sellers.ToListAsync();
        var userManager = HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var fixedCount = 0;
        var messages = new List<string>();

        foreach (var seller in sellers)
        {
            var user = await userManager.FindByIdAsync(seller.UserId);
            if (user == null)
            {
                // User missing, recreate
                var email = seller.Email;
                if (string.IsNullOrEmpty(email))
                {
                    // Generate email from ShopName if missing
                    var safeName = new string(seller.ShopName.Where(char.IsLetterOrDigit).ToArray()).ToLower();
                    if (string.IsNullOrEmpty(safeName)) safeName = "seller";
                    
                    email = $"{safeName}@sparkle.local";
                    
                    // Ensure uniqueness
                    int counter = 1;
                    while (await userManager.FindByEmailAsync(email) != null)
                    {
                        email = $"{safeName}{counter}@sparkle.local";
                        counter++;
                    }

                    seller.Email = email; // Update seller email
                }

                user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true,
                        FullName = seller.ShopName,
                        IsSeller = true,
                        PhoneNumber = seller.MobileNumber
                    };

                    var result = await userManager.CreateAsync(user, "Vendor@123");
                    if (result.Succeeded)
                    {
                        await userManager.AddToRoleAsync(user, "Seller");
                        seller.UserId = user.Id;
                        fixedCount++;
                        messages.Add($"Fixed seller {seller.ShopName}: Created user {email}");
                    }
                    else
                    {
                        messages.Add($"Failed to fix seller {seller.ShopName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                    }
                }
                else
                {
                    seller.UserId = user.Id;
                    if (!await userManager.IsInRoleAsync(user, "Seller"))
                    {
                        await userManager.AddToRoleAsync(user, "Seller");
                    }
                    fixedCount++;
                    messages.Add($"Fixed seller {seller.ShopName}: Relinked to existing user {email}");
                }
            }
            else
            {
                // User exists, ensure Role
                if (!await userManager.IsInRoleAsync(user, "Seller"))
                {
                    await userManager.AddToRoleAsync(user, "Seller");
                    fixedCount++;
                    messages.Add($"Fixed seller {seller.ShopName}: Added Seller role");
                }
            }
        }

        if (fixedCount > 0)
        {
            await _db.SaveChangesAsync();
            TempData["Success"] = $"Repaired {fixedCount} seller accounts.";
        }
        else
        {
            TempData["Info"] = "All seller accounts appear to be healthy.";
        }

        return View("RepairReport", messages);
    }
}
