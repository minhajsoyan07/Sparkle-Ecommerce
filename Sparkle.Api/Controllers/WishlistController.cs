using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Users;
using Sparkle.Infrastructure;
using Sparkle.Domain.Identity;

namespace Sparkle.Api.Controllers;

[Authorize]
[Route("wishlist")]
public class WishlistController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public WishlistController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");

        var wishlistItems = await _context.UserWishlistItems
            .Include(w => w.Product)
                .ThenInclude(p => p.Images)
            .Where(w => w.UserId == user.Id)
            .OrderByDescending(w => w.AddedAt)
            .ToListAsync();

        return View(wishlistItems);
    }

    [HttpPost("add/{productId}")]
    public async Task<IActionResult> Add(int productId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Json(new { success = false, message = "Please login first" });

        // Check if already in wishlist
        var existing = await _context.UserWishlistItems
            .FirstOrDefaultAsync(w => w.UserId == user.Id && w.ProductId == productId);

        if (existing != null)
        {
            return Json(new { success = false, message = "Item already in wishlist" });
        }

        // Get product to save price
        var product = await _context.Products.FindAsync(productId);
        if (product == null) return Json(new { success = false, message = "Product not found" });

        var item = new UserWishlistItem
        {
            UserId = user.Id,
            ProductId = productId,
            AddedAt = DateTime.UtcNow,
            PriceWhenAdded = product.Price
        };

        _context.UserWishlistItems.Add(item);
        await _context.SaveChangesAsync();

        return Json(new { success = true, message = "Added to wishlist" });
    }

    [HttpPost("remove/{id}")]
    public async Task<IActionResult> Remove(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");

        var item = await _context.UserWishlistItems
            .FirstOrDefaultAsync(w => w.UserId == user.Id && w.Id == id);

        if (item != null)
        {
            _context.UserWishlistItems.Remove(item);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Item removed from wishlist";
        }

        return RedirectToAction(nameof(Index));
    }
}
