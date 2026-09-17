using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Controllers;
using Sparkle.Domain.Orders;
using Sparkle.Infrastructure;
using Sparkle.Api.Areas.Seller.Models;

namespace Sparkle.Api.Areas.Seller.Controllers;

[Area("Seller")]
[Authorize(Roles = "Seller")]
public class CustomerController : Controller
{
    private readonly ApplicationDbContext _db;

    public CustomerController(ApplicationDbContext db)
    {
        _db = db;
    }
    
    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException();

    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        
        if (seller == null) return RedirectToAction("Setup", "Store");

        // Get all order items for this seller
        var customers = await _db.OrderItems
            .Include(oi => oi.Order)
                .ThenInclude(o => o.User)
            .Where(oi => oi.ProductVariant != null && oi.ProductVariant.Product != null && oi.ProductVariant.Product.SellerId == seller.Id)
            .GroupBy(oi => oi.Order.UserId)
            .Select(g => new CustomerViewModel
            {
                UserId = g.Key,
                Name = g.First().Order.User != null ? (g.First().Order.User.FullName ?? "Guest") : "Guest",
                Email = g.First().Order.User != null ? g.First().Order.User.Email : "No Email",
                Phone = g.First().Order.User != null ? g.First().Order.User.PhoneNumber : "",
                TotalOrders = g.Select(oi => oi.OrderId).Distinct().Count(),
                TotalSpent = g.Sum(oi => oi.LineTotal),
                LastOrderDate = g.Max(oi => oi.Order.OrderDate)
            })
            .OrderByDescending(c => c.LastOrderDate)
            .ToListAsync();

        return View(customers);
    }

    public async Task<IActionResult> Details(string id)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        
        if (seller == null) return RedirectToAction("Setup", "Store");

        // Get customer details
        var customer = await _db.OrderItems
            .Include(oi => oi.Order)
                .ThenInclude(o => o.User)
            .Where(oi => oi.ProductVariant != null && oi.ProductVariant.Product != null && 
                         oi.ProductVariant.Product.SellerId == seller.Id && 
                         oi.Order.UserId == id)
            .GroupBy(oi => oi.Order.UserId)
            .Select(g => new CustomerViewModel
            {
                UserId = g.Key,
                Name = g.First().Order.User != null ? (g.First().Order.User.FullName ?? "Guest") : "Guest",
                Email = g.First().Order.User != null ? g.First().Order.User.Email : "No Email",
                Phone = g.First().Order.User != null ? g.First().Order.User.PhoneNumber : "",
                TotalOrders = g.Select(oi => oi.OrderId).Distinct().Count(),
                TotalSpent = g.Sum(oi => oi.LineTotal),
                LastOrderDate = g.Max(oi => oi.Order.OrderDate)
            })
            .FirstOrDefaultAsync();

        if (customer == null)
        {
            return NotFound();
        }

        return View(customer);
    }
}



