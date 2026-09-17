namespace Sparkle.Domain.Support;

public class Ticket
{
    public int Id { get; set; }
    public string UserId { get; set; } = null!;
    public required string Subject { get; set; }
    public required string Description { get; set; }
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? AssignedTo { get; set; }
    
    public ICollection<TicketReply> Replies { get; set; } = [];
    public ICollection<TicketAttachment> Attachments { get; set; } = [];
}

public class TicketReply
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public string UserId { get; set; } = null!;
    public bool IsStaff { get; set; } // Admin or Vendor
    public required string Message { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TicketAttachment
{
    public int Id { get; set; }
    public int TicketId { get; set; }
    public Ticket Ticket { get; set; } = null!;
    public required string FileUrl { get; set; }
    public required string FileName { get; set; }
    public long FileSize { get; set; }
    public DateTime UploadedAt { get; set; }
}

public enum TicketStatus
{
    Open,
    InProgress,
    Resolved,
    Closed
}

public enum TicketPriority
{
    Low,
    Normal,
    High,
    Urgent
}
