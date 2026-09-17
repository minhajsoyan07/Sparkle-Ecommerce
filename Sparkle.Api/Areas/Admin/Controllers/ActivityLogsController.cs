using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Sparkle.Infrastructure;
using Sparkle.Domain.System;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class ActivityLogsController : Controller
{
    private readonly ApplicationDbContext _db;

    public ActivityLogsController(ApplicationDbContext db)
    {
        _db = db;
    }

    // GET: Admin/ActivityLogs
    public async Task<IActionResult> Index(
        DateTime? startDate,
        DateTime? endDate,
        string? action,
        string? entityType,
        int page = 1,
        int pageSize = 50)
    {
        var query = _db.ActivityLogs
            .OrderByDescending(log => log.CreatedAt)
            .AsQueryable();

        // Filters
        if (startDate.HasValue)
            query = query.Where(l => l.CreatedAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(l => l.CreatedAt <= endDate.Value.AddDays(1));

        if (!string.IsNullOrEmpty(action))
            query = query.Where(l => l.Action != null && l.Action.Contains(action));

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(l => l.EntityType == entityType);

        // Pagination
        var totalCount = await query.CountAsync();
        var logs = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
        ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
        ViewBag.Action = action;
        ViewBag.EntityType = entityType;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        return View(logs);
    }
}
