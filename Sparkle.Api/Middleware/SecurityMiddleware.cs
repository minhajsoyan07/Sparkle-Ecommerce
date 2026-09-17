using Sparkle.Infrastructure;
using System.Threading.RateLimiting;

namespace Sparkle.Api.Middleware;

public class SecurityMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Add security headers
        AddSecurityHeaders(context);

        await _next(context);
    }

    private static void AddSecurityHeaders(HttpContext context)
    {
        var response = context.Response;

        // Content Security Policy
        if (!response.Headers.ContainsKey("Content-Security-Policy"))
        {
            response.Headers.Append("Content-Security-Policy",
                "default-src 'self'; " +
                "img-src 'self' data: https: blob:; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://cdn.tailwindcss.com https://code.jquery.com; " +
                "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://fonts.googleapis.com https://cdn.tailwindcss.com; " +
                "font-src 'self' data: https://cdn.jsdelivr.net https://fonts.gstatic.com; " +
                "connect-src 'self' ws: wss:; " +
                "frame-ancestors 'none';");
        }

        // XSS Protection
        response.Headers.Append("X-Content-Type-Options", "nosniff");
        response.Headers.Append("X-Frame-Options", "DENY");
        response.Headers.Append("X-XSS-Protection", "1; mode=block");

        // HTTPS Enforcement (if in production)
        if (!context.Request.IsHttps && context.Request.Host.Host != "localhost")
        {
            response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
        }

        // Referrer Policy
        response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");

        // Permissions Policy
        response.Headers.Append("Permissions-Policy",
            "geolocation=(), microphone=(), camera=()");
    }
}
