using Sparkle.Domain.System;

namespace Sparkle.Infrastructure.Services;

public interface ISupportService
{
    Task<SupportTicket> CreateTicketAsync(string userId, string subject, string description, string priority);
    Task<List<SupportTicket>> GetUserTicketsAsync(string userId);
    Task<List<SupportTicket>> GetAllTicketsAsync(string? status = null);
    Task<SupportTicket> GetTicketDetailsAsync(int ticketId);
    Task AddReplyAsync(int ticketId, string userId, string message, bool isStaff);
    Task UpdateTicketStatusAsync(int ticketId, string status);
}
