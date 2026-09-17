using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Models;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Sellers;
using Sparkle.Domain.System;
using Sparkle.Domain.Marketing;
using Sparkle.Domain.Support;
using Sparkle.Domain.Identity;
using Sparkle.Infrastructure;
using Microsoft.Extensions.Caching.Memory;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    public DashboardController(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IActionResult> Index(string sortOrder = "date")
    {
        var cacheKey = $"AdminDashboardStats_{sortOrder}";
        
        if (!_cache.TryGetValue(cacheKey, out DashboardStats? stats))
        {
            var now = DateTime.UtcNow;
            var today = now.Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);
            var weekAgo = now.AddDays(-7);

            try
            {
                stats = new DashboardStats();
                
                // Optimize category counts query
                var categoryCounts = await _db.Products
                    .AsNoTracking()
                    .Include(p => p.Category)
                    .Where(p => p.IsActive && p.Category != null)
                    .GroupBy(p => p.Category.Slug)
                    .Select(g => new { Slug = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Slug ?? "unknown", x => x.Count);

                stats.TotalUsers = await _db.Users.CountAsync(u => !u.IsSeller);
                stats.NewUsersToday = await _db.Users.CountAsync(u => !u.IsSeller && u.RegisteredAt >= today);
                stats.TotalSellers = await _db.Sellers.CountAsync();
                stats.ActiveSellers = await _db.Sellers.CountAsync(v => v.Status == Domain.Sellers.SellerStatus.Approved);
                stats.PendingSellers = await _db.Sellers.CountAsync(v => v.Status == Domain.Sellers.SellerStatus.Pending);
                
                stats.TotalProducts = await _db.Products.CountAsync();
                stats.ActiveProducts = await _db.Products.CountAsync(p => p.IsActive);
                stats.TotalCategories = await _db.Categories.CountAsync();
                
                stats.TotalOrders = await _db.Orders.AsNoTracking().CountAsync();
                stats.PendingOrders = await _db.Orders.AsNoTracking().CountAsync(o => o.Status == OrderStatus.Pending);
                stats.ProcessingOrders = await _db.Orders.AsNoTracking().CountAsync(o => o.Status == OrderStatus.Processing);
                stats.DeliveredOrders = await _db.Orders.AsNoTracking().CountAsync(o => o.Status == OrderStatus.Delivered);
                stats.TodayOrders = await _db.Orders.AsNoTracking().CountAsync(o => o.OrderDate >= today && o.OrderDate < today.AddDays(1));
                stats.WeekOrders = await _db.Orders.AsNoTracking().CountAsync(o => o.OrderDate >= weekAgo);
                
                stats.TotalRevenue = await _db.Orders.Where(o => o.Status != OrderStatus.Cancelled).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
                stats.TodayRevenue = await _db.Orders.Where(o => o.OrderDate >= today && o.OrderDate < today.AddDays(1) && o.Status != OrderStatus.Cancelled).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
                stats.MonthRevenue = await _db.Orders.Where(o => o.OrderDate >= monthStart && o.Status != OrderStatus.Cancelled).SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;

                // Dynamic query for Recent Orders
                var recentOrdersQuery = _db.Orders
                        .AsNoTracking()
                        .Include(o => o.User)
                        .Include(o => o.Seller)
                        .Include(o => o.OrderItems).ThenInclude(i => i.Seller)
                        .AsQueryable();

                recentOrdersQuery = sortOrder switch
                {
                    "amount" => recentOrdersQuery.OrderByDescending(o => o.TotalAmount),
                    "amount_asc" => recentOrdersQuery.OrderBy(o => o.TotalAmount),
                    "date_asc" => recentOrdersQuery.OrderBy(o => o.OrderDate),
                    _ => recentOrdersQuery.OrderByDescending(o => o.OrderDate)
                };

                stats.RecentOrders = await recentOrdersQuery
                        .Take(7)
                        .Select(o => new AdminRecentOrder
                        {
                            OrderId = o.Id,
                            OrderNumber = o.OrderNumber,
                            ProductName = o.OrderItems.Where(i => i.ProductName != null && i.ProductName != "").Select(i => i.ProductName).FirstOrDefault() ?? "Product",
                            CustomerName = o.User != null ? (o.User.FullName ?? o.User.Email ?? "Guest") : "Guest",
                            SellerName = o.Seller != null ? o.Seller.ShopName : (o.OrderItems.Where(i => i.Seller != null).Select(i => i.Seller!.ShopName).FirstOrDefault() ?? "Sparkle"),
                            Date = o.OrderDate,
                            Total = o.TotalAmount,
                            Status = o.Status.ToString()
                        }).ToListAsync();
                    
                stats.TopSellers = await _db.Sellers
                        .AsNoTracking()
                        .Select(v => new TopSeller
                        {
                            SellerId = v.Id,
                            ShopName = v.ShopName,
                            TotalProducts = _db.Products.Count(p => p.SellerId == v.Id),
                            TotalOrders = _db.OrderItems.Where(oi => oi.SellerId == v.Id).Select(oi => oi.OrderId).Distinct().Count(),
                            TotalRevenue = _db.OrderItems
                                .Where(oi => oi.SellerId == v.Id)
                                .Sum(oi => (decimal?)oi.TotalPrice) ?? 0m
                        })
                        .OrderByDescending(tv => tv.TotalRevenue)
                        .Take(5)
                        .ToListAsync();

                stats.RecentActivities = await _db.ActivityLogs
                        .AsNoTracking()
                        .Include(l => l.User)
                        .OrderByDescending(l => l.Timestamp)
                        .Take(10)
                        .ToListAsync();

                // Calculate Revenue Chart Data (Last 30 Days)
                var thirtyDaysAgo = today.AddDays(-30);
                var dailyRevenues = await _db.Orders
                    .AsNoTracking()
                    .Where(o => o.OrderDate >= thirtyDaysAgo && o.Status != OrderStatus.Cancelled)
                    .GroupBy(o => o.OrderDate.Date)
                    .Select(g => new 
                    { 
                        Date = g.Key, 
                        Revenue = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m,
                        Count = g.Count()
                    })
                    .ToListAsync();

                var chartData = new List<DailyRevenue>();
                for (int i = 0; i < 30; i++)
                {
                    var date = thirtyDaysAgo.AddDays(i);
                    var data = dailyRevenues.FirstOrDefault(d => d.Date == date);
                    chartData.Add(new DailyRevenue
                    {
                        Date = date.ToString("MMM dd"),
                        FullDate = date,
                        Revenue = data?.Revenue ?? 0m,
                        Orders = data?.Count ?? 0
                    });
                }
                stats.RevenueChart = chartData;

                // Calculate Sales Chart Data (Last 6 Months Online vs Offline)
                var sixMonthsAgo = new DateTime(now.Year, now.Month, 1).AddMonths(-5);
                var monthStartDates = Enumerable.Range(0, 6)
                    .Select(i => sixMonthsAgo.AddMonths(i))
                    .ToList();

                var monthlySales = await _db.Orders
                    .AsNoTracking()
                    .Where(o => o.OrderDate >= sixMonthsAgo && o.Status != OrderStatus.Cancelled)
                    .GroupBy(o => new { o.OrderDate.Year, o.OrderDate.Month, o.PaymentMethod })
                    .Select(g => new 
                    { 
                        Year = g.Key.Year, 
                        Month = g.Key.Month, 
                        PMethod = g.Key.PaymentMethod,
                        Total = g.Sum(o => (decimal?)o.TotalAmount) ?? 0m
                    })
                    .ToListAsync();

                var salesChartData = new List<SalesChartItem>();
                foreach (var date in monthStartDates)
                {
                    var monthData = monthlySales.Where(s => s.Year == date.Year && s.Month == date.Month).ToList();
                    
                    salesChartData.Add(new SalesChartItem
                    {
                        Month = date.ToString("MMM"),
                        Online = monthData.Where(s => s.PMethod != Sparkle.Domain.Orders.PaymentMethodType.CashOnDelivery).Sum(s => s.Total),
                        Offline = monthData.Where(s => s.PMethod == Sparkle.Domain.Orders.PaymentMethodType.CashOnDelivery).Sum(s => s.Total)
                    });
                }
                stats.SalesChart = salesChartData;

                stats.AverageOrderValue = stats.TotalOrders > 0 ? stats.TotalRevenue / stats.TotalOrders : 0m;

                // Fetch Active Banner
                stats.FeaturedBanner = await _db.Banners
                    .AsNoTracking()
                    .Where(b => b.IsActive && b.Position == "Dashboard")
                    .OrderBy(b => b.DisplayOrder)
                    .FirstOrDefaultAsync();

                if (stats.FeaturedBanner == null)
                {
                    stats.FeaturedBanner = await _db.Banners
                        .AsNoTracking()
                        .Where(b => b.IsActive)
                        .OrderBy(b => b.DisplayOrder)
                        .FirstOrDefaultAsync();
                }

                // Fetch Recent Disputes
                stats.RecentDisputes = await _db.Disputes
                    .AsNoTracking()
                    .Include(d => d.User)
                    .Where(d => d.Status == DisputeStatus.Opened || d.Status == DisputeStatus.UnderInvestigation)
                    .OrderByDescending(d => d.OpenedAt)
                    .Take(5)
                    .ToListAsync();

                // Set cache options
                var cacheEntryOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(5));

                // Save data in cache
                _cache.Set(cacheKey, stats, cacheEntryOptions);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Dashboard Error: {ex.Message}");
                var emptyStats = new DashboardStats();
                ModelState.AddModelError("", "An error occurred while loading dashboard statistics.");
                return View(emptyStats);
            }
        }
        
        // Pass sort order to view (doesn't need caching)
        ViewBag.CurrentSort = sortOrder;
        
        return View(stats);
    }
}

public class DashboardStats
{
    public int TotalUsers { get; set; }
    public int NewUsersToday { get; set; }
    public int TotalSellers { get; set; }
    public int ActiveSellers { get; set; }
    public int PendingSellers { get; set; }
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int TotalCategories { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ProcessingOrders { get; set; }
    public int DeliveredOrders { get; set; }
    public int TodayOrders { get; set; }
    public int WeekOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal MonthRevenue { get; set; }
    public decimal AverageOrderValue { get; set; }
    public List<AdminRecentOrder> RecentOrders { get; set; } = new();
    public List<TopSeller> TopSellers { get; set; } = new();
    
    // New Properties
    public List<DailyRevenue> RevenueChart { get; set; } = new();
    public List<SalesChartItem> SalesChart { get; set; } = new();
    public Banner? FeaturedBanner { get; set; }
    public List<ActivityLog> RecentActivities { get; set; } = new();
    public List<Sparkle.Domain.Support.Dispute> RecentDisputes { get; set; } = new();
}

public class AdminRecentOrder
{
    public int OrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string SellerName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class TopSeller
{
    public int SellerId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
}

public class DailyRevenue
{
    public string Date { get; set; } = string.Empty;
    public DateTime FullDate { get; set; }
    public decimal Revenue { get; set; }
    public int Orders { get; set; }
}

public class SalesChartItem 
{
    public string Month { get; set; } = string.Empty;
    public decimal Online { get; set; }
    public decimal Offline { get; set; }
}

