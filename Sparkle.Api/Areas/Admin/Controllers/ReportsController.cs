using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Support;
using Sparkle.Api.Services;
using System.Text;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReportsController> _logger;
    private readonly IPdfReportService _pdfReportService;

    public ReportsController(ApplicationDbContext db, ILogger<ReportsController> logger, IPdfReportService pdfReportService)
    {
        _db = db;
        _logger = logger;
        _pdfReportService = pdfReportService;
    }

    // GET: /Admin/Reports
    public IActionResult Index()
    {
        return View();
    }

    // GET: /Admin/Reports/Sales
    public async Task<IActionResult> Sales(DateTime? from, DateTime? to)
    {
        from ??= DateTime.Now.AddMonths(-1);
        to ??= DateTime.Now;

        var salesData = await _db.Orders
            .Where(o => o.OrderDate >= from && o.OrderDate <= to)
            .GroupBy(o => o.OrderDate.Date)
            .Select(g => new SalesReportItem
            {
                Date = g.Key,
                TotalOrders = g.Count(),
                TotalRevenue = g.Sum(o => o.TotalAmount),
                AverageOrderValue = g.Average(o => o.TotalAmount)
            })
            .OrderBy(x => x.Date)
            .ToListAsync();

        ViewBag.FromDate = from;
        ViewBag.ToDate = to;
        ViewBag.TotalRevenue = salesData.Sum(x => x.TotalRevenue);
        ViewBag.TotalOrders = salesData.Sum(x => x.TotalOrders);

        return View(salesData);
    }

    // GET: /Admin/Reports/Users
    public async Task<IActionResult> Users()
    {
        var userStats = new UserReportViewModel
        {
            TotalUsers = await _db.Users.CountAsync(),
            ActiveUsers = await _db.Users.CountAsync(u => u.IsActive),
            NewUsersThisMonth = await _db.Users.CountAsync(u => u.RegisteredAt >= DateTime.Now.AddMonths(-1)),
            UsersByRole = await _db.UserRoles
                .GroupBy(ur => ur.RoleId)
                .Select(g => new RoleCount
                {
                    RoleId = g.Key,
                    Count = g.Count()
                })
                .ToListAsync()
        };

        // Get role names
        var roles = await _db.Roles.ToListAsync();
        foreach (var stat in userStats.UsersByRole)
        {
            stat.RoleName = roles.FirstOrDefault(r => r.Id == stat.RoleId)?.Name ?? "Unknown";
        }

        return View(userStats);
    }

    // GET: /Admin/Reports/Sellers
    public async Task<IActionResult> Sellers()
    {
        var sellerPerformance = await _db.Sellers
            .Select(s => new SellerPerformanceViewModel
            {
                SellerId = s.Id,
                SellerName = s.ShopName,
                TotalProducts = _db.Products.Count(p => p.SellerId == s.Id),
                TotalSales = _db.Orders
                    .Where(o => o.OrderItems.Any(i => i.Product != null && i.Product.SellerId == s.Id))
                    .Sum(o => o.TotalAmount),
                TotalOrders = _db.Orders
                    .Count(o => o.OrderItems.Any(i => i.Product != null && i.Product.SellerId == s.Id)),
                Rating = 4.5m, // Would come from reviews
                Status = s.Status.ToString()
            })
            .OrderByDescending(s => s.TotalSales)
            .ToListAsync();

        return View(sellerPerformance);
    }

    // GET: /Admin/Reports/Export
    public async Task<IActionResult> Export(string type, DateTime? from, DateTime? to, string format = "csv")
    {
        from ??= DateTime.Now.AddMonths(-1);
        to ??= DateTime.Now;

        if (type == "sales")
        {
            if (format == "pdf")
            {
                var aggregatedData = await _db.Orders
                    .Where(o => o.OrderDate >= from && o.OrderDate <= to)
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new SalesReportItem
                    {
                        Date = g.Key,
                        TotalOrders = g.Count(),
                        TotalRevenue = g.Sum(o => o.TotalAmount),
                        AverageOrderValue = g.Average(o => o.TotalAmount)
                    })
                    .OrderBy(x => x.Date)
                    .ToListAsync();

                var pdfBytes = _pdfReportService.GenerateSalesReportPdf(from.Value, to.Value, aggregatedData);
                return File(pdfBytes, "application/pdf", $"sales-report-{DateTime.Now:yyyy-MM-dd}.pdf");
            }

            var salesData = await _db.Orders
                .Where(o => o.OrderDate >= from && o.OrderDate <= to)
                .Include(o => o.User)
                .Select(o => new
                {
                    OrderNumber = o.OrderNumber ?? $"ORD-{o.Id}",
                    CustomerName = o.User != null ? o.User.FullName : "Guest",
                    o.TotalAmount,
                    Status = o.Status.ToString(),
                    o.OrderDate
                })
                .ToListAsync();

            var csv = new StringBuilder();
            csv.AppendLine("Order Number,Customer,Amount,Status,Date");
            foreach (var item in salesData)
            {
                csv.AppendLine($"{item.OrderNumber},{item.CustomerName},{item.TotalAmount},{item.Status},{item.OrderDate:yyyy-MM-dd}");
            }

            return File(Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"sales-report-{DateTime.Now:yyyy-MM-dd}.csv");
        }

        return RedirectToAction("Index");
    }

    public class SalesReportItem
    {
        public DateTime Date { get; set; }
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal AverageOrderValue { get; set; }
    }

    public class UserReportViewModel
    {
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int NewUsersThisMonth { get; set; }
        public List<RoleCount> UsersByRole { get; set; } = new();
    }

    public class RoleCount
    {
        public string RoleId { get; set; } = "";
        public string RoleName { get; set; } = "";
        public int Count { get; set; }
    }

    public class SellerPerformanceViewModel
    {
        public int SellerId { get; set; }
        public string SellerName { get; set; } = "";
        public int TotalProducts { get; set; }
        public decimal TotalSales { get; set; }
        public int TotalOrders { get; set; }
        public decimal Rating { get; set; }
        public string Status { get; set; } = "";
    }
    // GET: /Admin/Reports/Issues
    public async Task<IActionResult> Issues(string status = "Pending")
    {
        var reports = await _db.Reports
            .Include(r => r.Reporter)
            .Include(r => r.Product)
            .Include(r => r.Seller)
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        
        ViewBag.Status = status;
        return View(reports);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resolve(int id, string resolution, string notes, bool suspendProduct = false)
    {
        var report = await _db.Reports
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == id);
            
        if (report == null) return NotFound();
        
        // If admin chooses to suspend the product
        if (suspendProduct && report.ProductId.HasValue && report.Product != null)
        {
            var product = report.Product;
            product.ModerationStatus = Domain.Catalog.ProductModerationStatus.Suspended;
            product.IsActive = false;
            product.ModerationNotes = $"Suspended due to user report: {report.Reason}. {notes}";
            product.ModeratedAt = DateTime.UtcNow;
            product.ModeratedBy = User.Identity?.Name;
            
            _db.Products.Update(product);
        }
        
        report.Status = resolution; // Resolved, Dismissed
        report.ResolutionNotes = notes;
        report.ResolvedAt = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        
        TempData["Success"] = suspendProduct 
            ? "Report resolved and product suspended successfully." 
            : "Report resolved successfully.";
            
        return RedirectToAction(nameof(Issues));
    }
}
