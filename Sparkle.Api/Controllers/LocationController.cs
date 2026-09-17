using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LocationController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public LocationController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("zones")]
        public async Task<IActionResult> GetZones()
        {
            return Ok(await _context.DeliveryZones.Where(z => z.IsActive).ToListAsync());
        }

        [HttpGet("areas")]
        public async Task<IActionResult> GetAreas(int? zoneId, string? query)
        {
            var q = _context.DeliveryAreas.Include(a => a.Zone).AsQueryable();
            
            if (zoneId.HasValue) 
                q = q.Where(a => a.ZoneId == zoneId);
            
            if (!string.IsNullOrEmpty(query)) 
                q = q.Where(a => a.Name.Contains(query));
            
            var areas = await q.Take(50).ToListAsync();
            
            return Ok(areas.Select(a => new 
            { 
                a.Id, 
                a.Name, 
                a.District, 
                a.PostCode,
                ZoneName = a.Zone.Name,
                ZoneId = a.ZoneId,
                DeliveryCharge = a.Zone.BaseCharge
            }));
        }
    }
}
