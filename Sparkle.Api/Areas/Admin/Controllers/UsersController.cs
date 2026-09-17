using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Identity;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class UsersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(string q, string status, int page = 1)
    {
        ViewBag.ActiveCount = await _db.Users.CountAsync(u => u.IsActive);
        ViewBag.BlockedCount = await _db.Users.CountAsync(u => !u.IsActive);
        
        var pageSize = 20;
        var query = _db.Users.AsQueryable();

        // Filter out Sellers to ensure this view only shows Customers
        query = query.Where(u => !u.IsSeller);

        // Search
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            query = query.Where(u => (u.FullName != null && u.FullName.Contains(q)) || (u.Email != null && u.Email.Contains(q)) || (u.PhoneNumber != null && u.PhoneNumber.Contains(q)));
        }

        // Filter
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (status == "active")
                query = query.Where(u => u.IsActive);
            else if (status == "blocked")
                query = query.Where(u => !u.IsActive);
        }

        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var users = await query
            .OrderByDescending(u => u.RegisteredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.TotalUsers = totalItems;
        ViewBag.SearchQuery = q;
        ViewBag.CurrentStatus = status;

        return View(users);
    }

    /// <summary>
    /// View detailed user information including order history
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            return NotFound();
        }

        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // If user is a seller, redirect to Seller Details for proper context
        if (user.IsSeller)
        {
            var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == id);
            if (seller != null)
            {
                return RedirectToAction("Details", "Sellers", new { id = seller.Id });
            }
        }

        // Get user's order history with product details
        var orders = await _db.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.UserId == id)
            .OrderByDescending(o => o.OrderDate)
            .Take(20)
            .ToListAsync();

        // Get statistics
        ViewBag.TotalOrders = await _db.Orders.CountAsync(o => o.UserId == id);
        ViewBag.TotalSpent = await _db.Orders
            .Where(o => o.UserId == id && o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
            .SumAsync(o => o.TotalAmount);
        ViewBag.WishlistItems = await _db.Set<Sparkle.Domain.Orders.Wishlist>()
            .Where(w => w.UserId == id)
            .SelectMany(w => w.Items)
            .CountAsync();
        ViewBag.ReviewsWritten = await _db.ProductReviews.CountAsync(r => r.UserId == id);
        ViewBag.Orders = orders;

        // Get user roles
        var roles = await _userManager.GetRolesAsync(user);
        ViewBag.UserRoles = roles;

        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        
        TempData["Success"] = $"User {(user.IsActive ? "activated" : "blocked")} successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Reset user password to a temporary password
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Generate a temporary password
        var tempPassword = GenerateTemporaryPassword();
        
        // Remove existing password and set new one
        var removeResult = await _userManager.RemovePasswordAsync(user);
        if (!removeResult.Succeeded)
        {
            TempData["Error"] = "Failed to reset password: " + string.Join(", ", removeResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Details), new { id });
        }

        var addResult = await _userManager.AddPasswordAsync(user, tempPassword);
        if (!addResult.Succeeded)
        {
            TempData["Error"] = "Failed to set new password: " + string.Join(", ", addResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Details), new { id });
        }

        // Reset lockout if applicable
        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);

        TempData["Success"] = $"Password has been reset successfully. Temporary password: {tempPassword}";
        TempData["TempPassword"] = tempPassword;
        
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Suspend a user account (lock them out)
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(string id, string reason)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        // Lock out for 100 years (effectively permanent)
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        user.IsActive = false;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"User account has been suspended. Reason: {reason ?? "No reason provided"}";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Activate a suspended user account
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        await _userManager.SetLockoutEndDateAsync(user, null);
        await _userManager.ResetAccessFailedCountAsync(user);
        user.IsActive = true;
        await _db.SaveChangesAsync();

        TempData["Success"] = "User account has been activated successfully.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var user = await _db.Users.FindAsync(id);
        if (user == null)
        {
            return NotFound();
        }
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, ApplicationUser user)
    {
        if (id != user.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                var userInDb = await _db.Users.FindAsync(id);
                if (userInDb == null)
                {
                    return NotFound();
                }

                userInDb.PhoneNumber = user.PhoneNumber;
                userInDb.FullName = user.FullName;
                // Add other editable properties if needed
                
                _db.Update(userInDb);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(user.Id))
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
        return View(user);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await _db.Users.FindAsync(id);
        if (user != null)
        {
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            TempData["Success"] = "User deleted successfully.";
        }
        return RedirectToAction(nameof(Index));
    }

    private bool UserExists(string id)
    {
        return _db.Users.Any(e => e.Id == id);
    }

    private string GenerateTemporaryPassword()
    {
        // Generate a secure temporary password
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
        const string specialChars = "!@#$%&*";
        var random = new Random();
        
        var password = new char[12];
        for (int i = 0; i < 10; i++)
        {
            password[i] = chars[random.Next(chars.Length)];
        }
        // Add a special char and number to meet password requirements
        password[10] = specialChars[random.Next(specialChars.Length)];
        password[11] = '1';
        
        return new string(password);
    }
}
