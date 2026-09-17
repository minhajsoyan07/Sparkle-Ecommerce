using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Identity;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Profile.Controllers;

[Area("Profile")]
[Authorize]
[Route("profile/wallet")]
public class WalletController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public WalletController(UserManager<ApplicationUser> userManager, ApplicationDbContext context)
    {
        _userManager = userManager;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");

        var wallet = await _context.UserWallets
            .FirstOrDefaultAsync(w => w.UserId == user.Id);

        if (wallet == null)
        {
            // Auto-create wallet if missing
            wallet = new Domain.Wallets.UserWallet
            {
                UserId = user.Id,
                Balance = 0,
                IsActive = true
            };
            _context.UserWallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        var transactions = await _context.WalletTransactions
            .Where(t => t.UserId == user.Id)
            .OrderByDescending(t => t.TransactionDate)
            .Take(20)
            .ToListAsync();

        ViewBag.Transactions = transactions;
        return View(wallet);
    }
}
