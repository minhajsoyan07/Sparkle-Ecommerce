using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class PaymentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(ApplicationDbContext db, ILogger<PaymentsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    // GET: /Admin/Payments
    public async Task<IActionResult> Index(string? status, DateTime? from, DateTime? to, int page = 1)
    {
        var query = _db.Orders
            .Include(o => o.User)
            .Include(o => o.OrderItems)
            .Where(o => o.Status != Sparkle.Domain.Orders.OrderStatus.Pending)
            .OrderByDescending(o => o.OrderDate)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<Sparkle.Domain.Orders.OrderStatus>(status, out var orderStatus))
        {
            query = query.Where(o => o.Status == orderStatus);
        }

        if (from.HasValue)
        {
            query = query.Where(o => o.OrderDate >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(o => o.OrderDate <= to.Value.AddDays(1));
        }

        var pageSize = 20;
        var totalItems = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

        var transactions = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new TransactionViewModel
            {
                OrderId = o.Id,
                OrderNumber = o.OrderNumber ?? $"ORD-{o.Id}",
                CustomerName = o.User != null ? (o.User.FullName ?? "Guest") : "Guest",
                Amount = o.TotalAmount,
                Status = o.Status.ToString(),
                PaymentMethod = o.PaymentMethod.ToString(),
                Date = o.OrderDate
            })
            .ToListAsync();

        ViewBag.TotalRevenue = await _db.Orders
            .Where(o => o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
            .SumAsync(o => o.TotalAmount);

        ViewBag.PendingPayouts = await _db.Orders
            .Where(o => o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
            .SelectMany(o => o.OrderItems)
            .SumAsync(i => i.SellerEarning);

        ViewBag.TotalCommission = await _db.Orders
            .Where(o => o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
            .SelectMany(o => o.OrderItems)
            .SumAsync(i => i.PlatformCommissionAmount);

        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = totalPages;
        ViewBag.StatusFilter = status;
        ViewBag.FromDate = from;
        ViewBag.ToDate = to;

        return View(transactions);
    }

    // GET: /Admin/Payments/Details/5
    public async Task<IActionResult> Details(int id)
    {
        var order = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
            .Include(o => o.ShippingAddress)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound();
        }

        return View(order);
    }

    // GET: /Admin/Payments/Commissions
    public async Task<IActionResult> Commissions()
    {
        var sellerCommissions = await _db.Sellers
            .Select(s => new SellerCommissionViewModel
            {
                SellerId = s.Id,
                SellerName = s.ShopName,
                TotalSales = _db.Orders
                    .Where(o => o.OrderItems.Any(i => i.Product != null && i.Product.SellerId == s.Id))
                    .Where(o => o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
                    .Sum(o => o.TotalAmount),
                Commission = _db.Orders
                    .Where(o => o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
                    .SelectMany(o => o.OrderItems)
                    .Where(i => i.Product != null && i.Product.SellerId == s.Id)
                    .Sum(i => i.PlatformCommissionAmount),
                PayoutAmount = _db.Orders
                    .Where(o => o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
                    .SelectMany(o => o.OrderItems)
                    .Where(i => i.Product != null && i.Product.SellerId == s.Id)
                    .Sum(i => i.SellerEarning),
                OrderCount = _db.Orders
                    .Count(o => o.OrderItems.Any(i => i.Product != null && i.Product.SellerId == s.Id) 
                        && o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
            })
            .Where(s => s.TotalSales > 0)
            .OrderByDescending(s => s.TotalSales)
            .ToListAsync();

        return View(sellerCommissions);
    }

    // GET: /Admin/Payments/Payouts
    public async Task<IActionResult> Payouts()
    {
        var pendingPayouts = await _db.Sellers
            .Select(s => new PayoutViewModel
            {
                SellerId = s.Id,
                SellerName = s.ShopName,
                Email = s.Email ?? "",
                BankDetails = s.BusinessAddress ?? "Not provided",
                PendingAmount = _db.Orders
                    .Where(o => o.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
                    .SelectMany(o => o.OrderItems)
                    .Where(i => i.Product != null && i.Product.SellerId == s.Id)
                    .Sum(i => i.SellerEarning),
                LastPayout = null // Would come from a PayoutHistory table
            })
            .Where(p => p.PendingAmount > 0)
            .OrderByDescending(p => p.PendingAmount)
            .ToListAsync();

        return View(pendingPayouts);
    }

    public class TransactionViewModel
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = "";
        public string CustomerName { get; set; } = "";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
        public string PaymentMethod { get; set; } = "";
        public DateTime Date { get; set; }
    }

    public class SellerCommissionViewModel
    {
        public int SellerId { get; set; }
        public string SellerName { get; set; } = "";
        public decimal TotalSales { get; set; }
        public decimal Commission { get; set; }
        public decimal PayoutAmount { get; set; }
        public int OrderCount { get; set; }
    }

    public class PayoutViewModel
    {
        public int SellerId { get; set; }
        public string SellerName { get; set; } = "";
        public string Email { get; set; } = "";
        public string BankDetails { get; set; } = "";
        public decimal PendingAmount { get; set; }
        public DateTime? LastPayout { get; set; }
    }
}
