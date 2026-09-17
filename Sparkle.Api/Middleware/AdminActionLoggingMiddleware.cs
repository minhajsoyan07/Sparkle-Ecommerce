using Sparkle.Infrastructure;
using Sparkle.Domain.System;
using Microsoft.AspNetCore.Identity;
using Sparkle.Domain.Identity;

namespace Sparkle.Api.Middleware;

public class AdminActionLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AdminActionLoggingMiddleware> _logger;

    public AdminActionLoggingMiddleware(RequestDelegate next, ILogger<AdminActionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        // Only log Admin area actions
        if (!context.Request.Path.StartsWithSegments("/Admin"))
        {
            await _next(context);
            return;
        }

        // Only log POST, PUT, DELETE (data modification actions)
        if (context.Request.Method == "GET" || context.Request.Method == "HEAD")
        {
            await _next(context);
            return;
        }

        var user = await userManager.GetUserAsync(context.User);
        if (user == null || !context.User.IsInRole("Admin"))
        {
            await _next(context);
            return;
        }

        var action = context.Request.Path.Value ?? "";
        var method = context.Request.Method;
        var ipAddress = context.Connection.RemoteIpAddress?.ToString();

        // Capture request body for important actions
        string? requestBody = null;
        if (ShouldLogRequestBody(action))
        {
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync();
            context.Request.Body.Position = 0;
        }

        // Execute the action
        await _next(context);

        // Log after action completes
        var statusCode = context.Response.StatusCode;
        var success = statusCode >= 200 && statusCode < 400;

        try
        {
            var log = new ActivityLog
            {
                UserId = user.Id,
                Action = $"{method} {action}",
                EntityType = ExtractEntityType(action) ?? "Unknown",
                EntityId = ExtractEntityId(action),
                IpAddress = ipAddress,
                UserAgent = context.Request.Headers["User-Agent"].ToString(),
                Details = requestBody,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            db.ActivityLogs.Add(log);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to log admin action: {Action}", action);
        }
    }

    private static bool ShouldLogRequestBody(string action)
    {
        var importantPaths = new[] { "/create", "/edit", "/delete", "/approve", "/reject", "/suspend" };
        return importantPaths.Any(p => action.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private static string? ExtractEntityType(string path)
    {
        // Extract entity type from path like /Admin/Products/Edit/5 -> Products
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length >= 2 ? segments[1] : null;
    }

    private static string? ExtractEntityId(string path)
    {
        // Extract ID from path like /Admin/Products/Edit/5 -> 5
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length >= 4 && int.TryParse(segments[3], out _))
            return segments[3];
        return null;
    }
}
