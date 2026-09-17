using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;
using Sparkle.Domain.Support;
using System.Security.Claims;

namespace Sparkle.Api.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DisputesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DisputesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string status = "All")
        {
            var query = _context.Disputes
                .Include(d => d.User)
                .Include(d => d.Order)
                .AsQueryable();

            if (status != "All")
            {
                if (Enum.TryParse<DisputeStatus>(status, out var disputeStatus))
                {
                    query = query.Where(d => d.Status == disputeStatus);
                }
            }

            var disputes = await query.OrderByDescending(d => d.OpenedAt).ToListAsync();
            ViewBag.CurrentStatus = status;
            return View(disputes);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var dispute = await _context.Disputes
                .Include(d => d.User)
                .Include(d => d.Seller)
                .Include(d => d.Order).ThenInclude(o => o.OrderItems)
                .Include(d => d.Notes).ThenInclude(n => n.Author)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (dispute == null) return NotFound();
            return View(dispute);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id, DisputeResolution resolution, string resolutionDetails, decimal? refundAmount)
        {
            var dispute = await _context.Disputes.FindAsync(id);
            if (dispute == null) return NotFound();

            dispute.ResolutionType = resolution;
            dispute.ResolutionDetails = resolutionDetails;
            dispute.RefundAmount = refundAmount;
            dispute.Status = DisputeStatus.Resolved;
            dispute.ResolvedAt = DateTime.UtcNow;
            dispute.ResolvedBy = User.Identity?.Name ?? "Admin";
            dispute.ClosedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Dispute resolved successfully.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var dispute = await _context.Disputes.FindAsync(id);
            if (dispute == null) return NotFound();

            dispute.Status = DisputeStatus.Rejected;
            dispute.ResolutionType = DisputeResolution.NoAction;
            dispute.ResolutionDetails = reason;
            dispute.ResolvedAt = DateTime.UtcNow;
            dispute.ResolvedBy = User.Identity?.Name ?? "Admin";
            dispute.ClosedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            TempData["Success"] = "Dispute rejected.";
            return RedirectToAction(nameof(Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddNote(int id, string content, bool isInternal)
        {
            var dispute = await _context.Disputes.FindAsync(id);
            if (dispute == null) return NotFound();

            var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            var note = new DisputeNote
            {
                DisputeId = id,
                AuthorId = currentUserId ?? string.Empty, // Should be valid in real auth scenario
                Content = content,
                IsInternal = isInternal,
                PostedAt = DateTime.UtcNow
            };

            _context.DisputeNotes.Add(note);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
