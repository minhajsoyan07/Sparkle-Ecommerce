using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Sparkle.Api.Controllers;

[AllowAnonymous]
[Route("settings")]
public class SettingsController : Controller
{
    [HttpGet("")]
    public IActionResult Index()
    {
        var language = Request.Cookies["sparkle-lang"] ?? "en";
        var model = new SettingsViewModel
        {
            Language = language == "bn" ? "bn" : "en"
        };

        return View(model);
    }

    [HttpPost("language")]
    public IActionResult ChangeLanguage(string language, string? returnUrl)
    {
        var normalized = language == "bn" ? "bn" : "en";

        Response.Cookies.Append("sparkle-lang", normalized, new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            HttpOnly = false,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax
        });

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index");
    }

    public class SettingsViewModel
    {
        public string Language { get; set; } = "en";
    }
}
