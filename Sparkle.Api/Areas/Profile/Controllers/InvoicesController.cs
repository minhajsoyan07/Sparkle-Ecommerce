using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Sparkle.Api.Services;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Identity;

namespace Sparkle.Api.Areas.Profile.Controllers;

[Area("Profile")]
[Authorize]
public class InvoicesController : Controller
{
    private readonly IInvoiceService _invoiceService;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<InvoicesController> _logger;

    public InvoicesController(IInvoiceService invoiceService, ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILogger<InvoicesController> logger)
    {
        _invoiceService = invoiceService;
        _db = db;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Auth");
        }

        var invoices = await _db.Orders
            .Where(o => o.UserId == user.Id && o.PaymentStatus == Domain.Orders.PaymentStatus.Paid)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new InvoiceListItem
            {
                OrderId = o.Id,
                InvoiceNumber = "INV-" + o.Id.ToString("D6"),
                InvoiceDate = o.CreatedAt,
                TotalAmount = o.TotalAmount,
                OrderStatus = o.Status.ToString()
            })
            .ToListAsync();

        return View(invoices);
    }

    public class InvoiceListItem
    {
        public int OrderId { get; set; }
        public string InvoiceNumber { get; set; } = "";
        public DateTime InvoiceDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string OrderStatus { get; set; } = "";
    }

    // GET: /Profile/Invoices/Download/5
    [HttpGet("Profile/Invoices/Download/{orderId}")]
    public async Task<IActionResult> Download(int orderId)
    {
        try
        {
            var pdfBytes = await _invoiceService.GenerateOrderInvoiceAsync(orderId);
            var fileName = $"Invoice_Order_{orderId}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            
            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating invoice for order {OrderId}", orderId);
            TempData["Error"] = "Unable to generate invoice. Please try again.";
            return RedirectToAction("Details", "Orders", new { id = orderId });
        }
    }

    // GET: /Profile/Invoices/View/5
    [HttpGet("Profile/Invoices/View/{orderId}")]
    public async Task<IActionResult> View(int orderId)
    {
        try
        {
            var pdfBytes = await _invoiceService.GenerateOrderInvoiceAsync(orderId);
            return File(pdfBytes, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error viewing invoice for order {OrderId}", orderId);
            return StatusCode(500, "Error generating invoice");
        }
    }
}
