using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sparkle.Domain.System;
using Sparkle.Infrastructure.Services;

namespace Sparkle.Api.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class TicketsController : Controller
{
    private readonly ISupportService _supportService;

    public TicketsController(ISupportService supportService)
    {
        _supportService = supportService;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    public async Task<IActionResult> Index(string? status)
    {
        var tickets = await _supportService.GetAllTicketsAsync(status);
        ViewBag.CurrentStatus = status;
        return View(tickets);
    }

    public async Task<IActionResult> Details(int id)
    {
         try 
        {
            var ticket = await _supportService.GetTicketDetailsAsync(id);
            return View(ticket);
        }
        catch(KeyNotFoundException)
        {
            return NotFound(); 
        }
    }

    [HttpPost]
    public async Task<IActionResult> Reply(int id, string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return RedirectToAction(nameof(Details), new { id });
        
        await _supportService.AddReplyAsync(id, GetUserId(), message, isStaff: true);
        TempData["Success"] = "Reply sent";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        await _supportService.UpdateTicketStatusAsync(id, status);
        TempData["Success"] = "Status updated";
        return RedirectToAction(nameof(Details), new { id });
    }
}
