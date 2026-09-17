using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sparkle.Domain.Configuration;
using System.ComponentModel.DataAnnotations;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
[Route("admin/settings")]
public class SettingsController : Controller
{
    private readonly ISettingsService _settingsService;

    public SettingsController(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet]
    public IActionResult Index() => RedirectToAction("General");

    [HttpGet("general")]
    public async Task<IActionResult> General()
    {
        var model = new GeneralSettingsViewModel
        {
            SiteName = await _settingsService.GetValueAsync("SiteTitle", "Sparkle"),
            SiteDescription = await _settingsService.GetValueAsync("SiteDescription", "Your favorite e-commerce store"),
            SupportEmail = await _settingsService.GetValueAsync("SupportEmail", "support@sparkle.local"),
            SupportPhone = await _settingsService.GetValueAsync("SupportPhone", "+880 1XXX-XXXXXX"),
            OfficeAddress = await _settingsService.GetValueAsync("OfficeAddress", "Dhaka, Bangladesh"),
            FacebookUrl = await _settingsService.GetValueAsync("FacebookUrl", "https://facebook.com/sparkle"),
            InstagramUrl = await _settingsService.GetValueAsync("InstagramUrl", "https://instagram.com/sparkle")
        };
        return View(model);
    }

    [HttpPost("general")]
    public async Task<IActionResult> SaveGeneral(GeneralSettingsViewModel model)
    {
        if (!ModelState.IsValid) return View("General", model);

        await _settingsService.SetValueAsync("SiteTitle", model.SiteName, "General");
        await _settingsService.SetValueAsync("SiteDescription", model.SiteDescription, "General");
        await _settingsService.SetValueAsync("SupportEmail", model.SupportEmail, "General");
        await _settingsService.SetValueAsync("SupportPhone", model.SupportPhone, "General");
        await _settingsService.SetValueAsync("OfficeAddress", model.OfficeAddress, "General");
        await _settingsService.SetValueAsync("FacebookUrl", model.FacebookUrl, "Social");
        await _settingsService.SetValueAsync("InstagramUrl", model.InstagramUrl, "Social");

        TempData["Success"] = "General settings updated successfully";
        return RedirectToAction("General");
    }

    [HttpGet("payments")]
    public async Task<IActionResult> Payments()
    {
        var model = new PaymentSettingsViewModel
        {
            Currency = await _settingsService.GetValueAsync("Currency", "BDT"),
            EnableCOD = await _settingsService.GetValueAsync("EnableCOD", true),
            EnableBkash = await _settingsService.GetValueAsync("EnableBkash", true),
            BkashMerchantNumber = await _settingsService.GetValueAsync("BkashMerchantNumber", ""),
            EnableStripe = await _settingsService.GetValueAsync("EnableStripe", false),
            StripePublishableKey = await _settingsService.GetValueAsync("StripePublishableKey", ""),
            StripeSecretKey = await _settingsService.GetValueAsync("StripeSecretKey", "")
        };
        return View(model);
    }

    [HttpPost("payments")]
    public async Task<IActionResult> SavePayments(PaymentSettingsViewModel model)
    {
        if (!ModelState.IsValid) return View("Payments", model);

        await _settingsService.SetValueAsync("Currency", model.Currency, "Payment");
        await _settingsService.SetValueAsync("EnableCOD", model.EnableCOD, "Payment");
        await _settingsService.SetValueAsync("EnableBkash", model.EnableBkash, "Payment");
        await _settingsService.SetValueAsync("BkashMerchantNumber", model.BkashMerchantNumber, "Payment");
        await _settingsService.SetValueAsync("EnableStripe", model.EnableStripe, "Payment");
        await _settingsService.SetValueAsync("StripePublishableKey", model.StripePublishableKey, "Payment");
        await _settingsService.SetValueAsync("StripeSecretKey", model.StripeSecretKey, "Payment");

        TempData["Success"] = "Payment settings updated successfully";
        return RedirectToAction("Payments");
    }

    [HttpGet("shipping")]
    public async Task<IActionResult> Shipping()
    {
        var model = new ShippingSettingsViewModel
        {
            EnableLocalPickup = await _settingsService.GetValueAsync("EnableLocalPickup", false),
            EnableFreeShipping = await _settingsService.GetValueAsync("EnableFreeShipping", false),
            FreeShippingThreshold = await _settingsService.GetValueAsync("FreeShippingThreshold", 1000m)
        };
        return View(model);
    }

    [HttpPost("shipping")]
    public async Task<IActionResult> SaveShipping(ShippingSettingsViewModel model)
    {
        if (!ModelState.IsValid) return View("Shipping", model);

        await _settingsService.SetValueAsync("EnableLocalPickup", model.EnableLocalPickup, "Shipping");
        await _settingsService.SetValueAsync("EnableFreeShipping", model.EnableFreeShipping, "Shipping");
        await _settingsService.SetValueAsync("FreeShippingThreshold", model.FreeShippingThreshold, "Shipping");

        TempData["Success"] = "Shipping settings updated successfully";
        return RedirectToAction("Shipping");
    }

    [HttpGet("taxes")]
    public IActionResult Taxes() => View();

    [HttpGet("users")]
    public IActionResult Users() => View();

    [HttpGet("notifications")]
    public IActionResult Notifications() => View();
    
    [HttpGet("system")]
    public async Task<IActionResult> System()
    {
        var model = new SystemSettingsViewModel
        {
            MaintenanceMode = await _settingsService.GetValueAsync("MaintenanceMode", false),
            AllowRegistration = await _settingsService.GetValueAsync("AllowRegistration", true),
            RequireEmailConfirmation = await _settingsService.GetValueAsync("RequireEmailConfirmation", false)
        };
        return View(model);
    }

    [HttpPost("system")]
    public async Task<IActionResult> SaveSystem(SystemSettingsViewModel model)
    {
        if (!ModelState.IsValid) return View("System", model);

        await _settingsService.SetValueAsync("MaintenanceMode", model.MaintenanceMode, "System");
        await _settingsService.SetValueAsync("AllowRegistration", model.AllowRegistration, "System");
        await _settingsService.SetValueAsync("RequireEmailConfirmation", model.RequireEmailConfirmation, "System");

        TempData["Success"] = "System settings updated successfully";
        return RedirectToAction("System");
    }
}

public class GeneralSettingsViewModel
{
    [Required]
    public string SiteName { get; set; } = string.Empty;
    public string? SiteDescription { get; set; }
    
    [Required, EmailAddress]
    public string SupportEmail { get; set; } = string.Empty;
    public string? SupportPhone { get; set; }
    public string? OfficeAddress { get; set; }
    
    [Url]
    public string? FacebookUrl { get; set; }
    [Url]
    public string? InstagramUrl { get; set; }
}

public class PaymentSettingsViewModel
{
    public string Currency { get; set; } = "BDT";
    public bool EnableCOD { get; set; }
    public bool EnableBkash { get; set; }
    public string? BkashMerchantNumber { get; set; }
    public bool EnableStripe { get; set; }
    public string? StripePublishableKey { get; set; }
    public string? StripeSecretKey { get; set; }
}

public class ShippingSettingsViewModel
{
    public bool EnableLocalPickup { get; set; }
    public bool EnableFreeShipping { get; set; }
    public decimal FreeShippingThreshold { get; set; } = 1000m;
}

public class SystemSettingsViewModel
{
    public bool MaintenanceMode { get; set; }
    public bool AllowRegistration { get; set; }
    public bool RequireEmailConfirmation { get; set; }
}

