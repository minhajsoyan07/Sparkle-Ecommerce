using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Api.Models.ViewModels;
using System.Diagnostics;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class SystemController : Controller
{
    private readonly ApplicationDbContext _db;
    private static readonly DateTime _appStartTime = DateTime.UtcNow;

    public SystemController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Health()
    {
        // Get real database stats
        var totalUsers = await _db.Users.CountAsync();
        var totalOrders = await _db.Orders.CountAsync();
        var totalProducts = await _db.Products.CountAsync();
        var totalSellers = await _db.Sellers.CountAsync();
        
        // Calculate actual server uptime
        var uptime = DateTime.UtcNow - _appStartTime;

        // Get memory usage
        var process = Process.GetCurrentProcess();
        var memoryUsedMB = process.WorkingSet64 / (1024.0 * 1024.0);
        var gcMemoryMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0);
        
        // Estimate percentage (assuming 4GB available is reasonable for a web app)
        var estimatedMemoryPercent = (memoryUsedMB / 4096.0) * 100;

        // Check database connectivity
        var dbStatus = "Connected";
        try
        {
            await _db.Database.CanConnectAsync();
        }
        catch
        {
            dbStatus = "Disconnected";
        }

        var healthMetrics = new SystemHealthMetrics
        {
            DatabaseStatus = dbStatus,
            TotalUsers = totalUsers,
            TotalOrders = totalOrders,
            TotalProducts = totalProducts,
            TotalSellers = totalSellers,
            ServerUptime = uptime,
            MemoryUsage = Math.Round(estimatedMemoryPercent, 1),
            MemoryUsedMB = Math.Round(memoryUsedMB, 1),
            GCMemoryMB = Math.Round(gcMemoryMB, 1),
            CpuUsage = 0, // CPU usage requires complex calculation, keeping at 0
            
            // Additional metrics
            PendingOrders = await _db.Orders.CountAsync(o => o.Status == Domain.Orders.OrderStatus.Pending),
            TodayOrders = await _db.Orders.CountAsync(o => o.OrderDate.Date == DateTime.UtcNow.Date),
            ActiveSellers = await _db.Sellers.CountAsync(s => s.Status == Domain.Sellers.SellerStatus.Approved),
            OpenTickets = await _db.SupportTickets.CountAsync(t => t.Status == "Open")
        };

        return View(healthMetrics);
    }
}
