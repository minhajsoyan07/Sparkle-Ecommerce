using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using System.Security.Claims;

namespace Sparkle.Api.Areas.Seller.Controllers;

/// <summary>
/// Seller analytics and performance dashboard.
/// </summary>
[Area("Seller")]
[Authorize(Roles = "Seller")]
public class AnalyticsController : Controller
{
    private readonly ApplicationDbContext _db;

    public AnalyticsController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    private async Task<int?> GetSellerIdAsync()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        return seller?.Id;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string period = "30d")
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return RedirectToAction("Setup", "Dashboard");

        var now = DateTime.UtcNow;
        var startDate = period switch
        {
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            "90d" => now.AddDays(-90),
            "1y" => now.AddYears(-1),
            _ => now.AddDays(-30)
        };

        // Get seller's orders
        var orders = await _db.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .Where(o => o.OrderItems.Any(oi => oi.Product.SellerId == sellerId) && o.OrderDate >= startDate)
            .ToListAsync();

        // Calculate metrics
        var sellerItems = orders.SelectMany(o => o.OrderItems.Where(oi => oi.Product.SellerId == sellerId)).ToList();
        
        ViewBag.TotalRevenue = sellerItems.Sum(oi => oi.TotalPrice);
        ViewBag.TotalOrders = orders.Count;
        ViewBag.TotalItemsSold = sellerItems.Sum(oi => oi.Quantity);
        ViewBag.AverageOrderValue = orders.Any() ? sellerItems.Sum(oi => oi.TotalPrice) / orders.Count : 0;

        // Daily sales chart data
        var dailySales = orders
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new { Date = g.Key, Revenue = g.SelectMany(o => o.OrderItems.Where(oi => oi.Product.SellerId == sellerId)).Sum(oi => oi.TotalPrice) })
            .OrderBy(x => x.Date)
            .ToList();

        ViewBag.DailySalesLabels = dailySales.Select(x => x.Date.ToString("MMM d")).ToList();
        ViewBag.DailySalesData = dailySales.Select(x => x.Revenue).ToList();

        // Top products
        var topProducts = sellerItems
            .GroupBy(oi => oi.Product)
            .Select(g => new { Product = g.Key, TotalSold = g.Sum(oi => oi.Quantity), Revenue = g.Sum(oi => oi.TotalPrice) })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToList();

        ViewBag.TopProducts = topProducts;

        // Order status breakdown
        ViewBag.PendingOrders = orders.Count(o => o.Status == Domain.Orders.OrderStatus.Pending);
        ViewBag.ProcessingOrders = orders.Count(o => o.Status == Domain.Orders.OrderStatus.Processing);
        ViewBag.ShippedOrders = orders.Count(o => o.Status == Domain.Orders.OrderStatus.Shipped);
        ViewBag.DeliveredOrders = orders.Count(o => o.Status == Domain.Orders.OrderStatus.Delivered);
        ViewBag.CancelledOrders = orders.Count(o => o.Status == Domain.Orders.OrderStatus.Cancelled);

        // Reviews stats
        var reviews = await _db.ProductReviews
            .Include(r => r.Product)
            .Where(r => r.Product.SellerId == sellerId && r.ReviewDate >= startDate)
            .ToListAsync();

        ViewBag.TotalReviews = reviews.Count;
        ViewBag.AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 1) : 0;

        ViewBag.Period = period;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> SalesData(string period = "30d")
    {
        var sellerId = await GetSellerIdAsync();
        if (sellerId == null)
            return Json(new { error = "Seller not found" });

        var now = DateTime.UtcNow;
        var startDate = period switch
        {
            "7d" => now.AddDays(-7),
            "30d" => now.AddDays(-30),
            "90d" => now.AddDays(-90),
            _ => now.AddDays(-30)
        };

        var orders = await _db.Orders
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .Where(o => o.OrderItems.Any(oi => oi.Product.SellerId == sellerId) && o.OrderDate >= startDate)
            .ToListAsync();

        var dailySales = orders
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new 
            { 
                Date = g.Key.ToString("yyyy-MM-dd"), 
                Revenue = g.SelectMany(o => o.OrderItems.Where(oi => oi.Product.SellerId == sellerId)).Sum(oi => oi.TotalPrice),
                Orders = g.Count()
            })
            .OrderBy(x => x.Date)
            .ToList();

        return Json(dailySales);
    }
}
