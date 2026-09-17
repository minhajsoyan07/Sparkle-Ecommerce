using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using System.Security.Claims;

namespace Sparkle.Api.Controllers.Api;

[Route("api/addresses")]
[ApiController]
[Authorize]
public class AddressesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AddressesController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string? GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier);

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        
        if (address == null) return NotFound();

        _db.Addresses.Remove(address);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("{id}/set-default")]
    public async Task<IActionResult> SetDefault(int id)
    {
        var userId = GetUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (address == null) return NotFound();

        // Logic to set default
        var allAddresses = await _db.Addresses.Where(a => a.UserId == userId).ToListAsync();
        foreach(var a in allAddresses) 
        {
            a.IsDefault = (a.Id == id);
        }
        await _db.SaveChangesAsync();
        
        return Ok();
    }
}
