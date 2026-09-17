using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Models;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Support;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Controllers;

[Authorize]
public class ReportController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReportController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Create(string type, int id)
    {
        var model = new ReportViewModel
        {
            TargetType = type,
            TargetId = id
        };
        
        // Validate target
        if (type == "Product")
        {
             var product = await _context.Products
                .Include(p => p.Seller)
                .FirstOrDefaultAsync(p => p.Id == id);
             if (product == null) return NotFound();
             ViewBag.TargetName = product.Title;
             ViewBag.SellerName = product.Seller?.ShopName;
        }
        else if (type == "Seller")
        {
             var seller = await _context.Sellers.FindAsync(id);
             if (seller == null) return NotFound();
             ViewBag.TargetName = seller.ShopName;
        }
        else
        {
            return BadRequest("Invalid target type.");
        }
        
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ReportViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var report = new Report
        {
            ReporterId = user.Id,
            TargetType = model.TargetType,
            Reason = model.Reason,
            Description = model.Description,
            Status = "Pending"
        };

        if (model.TargetType == "Product")
        {
            report.ProductId = model.TargetId;
            // Get SellerId for convenience
            var product = await _context.Products.FindAsync(model.TargetId);
            report.SellerId = product?.SellerId;
        }
        else if (model.TargetType == "Seller")
        {
            report.SellerId = model.TargetId;
        }

        _context.Reports.Add(report);
        await _context.SaveChangesAsync();

        TempData["Success"] = "Report submitted successfully.";
        
        if (model.TargetType == "Product" && model.TargetId > 0)
        {
            return RedirectToAction("Product", "Home", new { id = model.TargetId });
        }
        else if (model.TargetType == "Seller" && model.TargetId > 0)
        {
            return RedirectToAction("SellerProfile", "Home", new { id = model.TargetId });
        }

        return RedirectToAction("Index", "Home");
    }
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var reports = await _context.Reports
            .Include(r => r.Product)
            .Include(r => r.Seller)
            .Where(r => r.ReporterId == user.Id)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return View(reports);
    }

    [HttpGet]
    public async Task<IActionResult> SearchProducts(string q)
    {
        if (string.IsNullOrWhiteSpace(q)) return Json(new List<object>());

        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Where(p => p.IsActive && p.Title.Contains(q))
            .OrderByDescending(p => p.Id)
            .Take(5)
            .Select(p => new
            {
                id = p.Id,
                title = p.Title,
                image = p.Images.OrderBy(i => i.SortOrder).Select(i => i.ImagePath).FirstOrDefault() ?? "/images/placeholder.jpg"
            })
            .ToListAsync();

        return Json(products);
    }
}
