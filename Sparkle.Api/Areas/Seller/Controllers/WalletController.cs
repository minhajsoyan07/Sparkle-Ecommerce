using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Sellers;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Seller.Controllers;

[Area("Seller")]
[Authorize(Roles = "Seller")]
[Route("seller/wallet")]
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
    public async Task<IActionResult> Earnings()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return RedirectToAction("Login", "Auth");

        var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);
        if (seller == null) return NotFound("Seller profile not found");

        var wallet = await _context.SellerWallets
            .FirstOrDefaultAsync(w => w.SellerId == seller.Id);

        if (wallet == null)
        {
            wallet = new Domain.Wallets.SellerWallet
            {
                SellerId = seller.Id,
                AvailableBalance = 0,
                PendingBalance = 0,
                IsActive = true
            };
            _context.SellerWallets.Add(wallet);
            await _context.SaveChangesAsync();
        }

        var transactions = await _context.WalletTransactions
            .Where(t => t.SellerId == seller.Id)
            .OrderByDescending(t => t.TransactionDate)
            .Take(50)
            .ToListAsync();

        ViewBag.Transactions = transactions;
        
        // Use ViewData["ActivePage"] for sidebar highlighting if your layout supports it
        ViewData["ActivePage"] = "Wallet";
        
        return View(wallet);
    }

    [HttpGet("withdraw")]
    public IActionResult Withdraw()
    {
        return View();
    }
}
