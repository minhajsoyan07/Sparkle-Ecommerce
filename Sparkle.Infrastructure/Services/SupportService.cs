using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.System;

namespace Sparkle.Infrastructure.Services;

public class SupportService : ISupportService
{
    private readonly ApplicationDbContext _db;

    public SupportService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<SupportTicket> CreateTicketAsync(string userId, string subject, string description, string priority)
    {
        var ticket = new SupportTicket
        {
            UserId = userId,
            Subject = subject,
            Description = description,
            Priority = priority,
            Status = "Open",
            TicketNumber = $"TKT-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 4).ToUpper()}",
            CreatedAt = DateTime.UtcNow,
            LastUpdatedAt = DateTime.UtcNow
        };

        _db.SupportTickets.Add(ticket);
        await _db.SaveChangesAsync();
        return ticket;
    }

    public async Task<List<SupportTicket>> GetUserTicketsAsync(string userId)
    {
        return await _db.SupportTickets
            .AsNoTracking()
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<SupportTicket>> GetAllTicketsAsync(string? status = null)
    {
        var query = _db.SupportTickets.AsNoTracking().Include(t => t.User).AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(t => t.Status == status);
        }

        return await query.OrderByDescending(t => t.CreatedAt).ToListAsync();
    }

    public async Task<SupportTicket> GetTicketDetailsAsync(int ticketId)
    {
        var ticket = await _db.SupportTickets
            .Include(t => t.Messages)
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Id == ticketId);
            
        return ticket ?? throw new KeyNotFoundException($"Ticket {ticketId} not found");
    }

    public async Task AddReplyAsync(int ticketId, string userId, string message, bool isStaff)
    {
        var ticket = await _db.SupportTickets.FindAsync(ticketId);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found");

        var reply = new TicketMessage
        {
            SupportTicketId = ticketId,
            UserId = userId,
            Message = message,
            IsStaffReply = isStaff,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.TicketMessages.Add(reply);

        // Update ticket status logic
        if (isStaff && ticket.Status == "Open")
        {
            ticket.Status = "InProgress";
            if (!ticket.FirstResponseAt.HasValue) ticket.FirstResponseAt = DateTime.UtcNow;
            ticket.AssignedTo = userId; // Auto-assign to responder? Or just leave open.
        }
        else if (!isStaff && ticket.Status == "Resolved")
        {
            ticket.Status = "Open"; // Re-open if user replies
        }
        
        ticket.LastUpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
    }

    public async Task UpdateTicketStatusAsync(int ticketId, string status)
    {
        var ticket = await _db.SupportTickets.FindAsync(ticketId);
        if (ticket == null) throw new KeyNotFoundException("Ticket not found");

        ticket.Status = status;
        ticket.LastUpdatedAt = DateTime.UtcNow;
        
        if (status == "Resolved")
        {
            ticket.ResolvedAt = DateTime.UtcNow;
        }
        else if (status == "Closed")
        {
            ticket.ClosedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }
}
