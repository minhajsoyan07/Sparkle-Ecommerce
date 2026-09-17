using Sparkle.Domain.Common;

namespace Sparkle.Domain.Identity;

/// <summary>
/// Admin sub-role types for fine-grained platform control
/// </summary>
public enum AdminRoleType
{
    SuperAdmin = 0,       // Full system access
    OperationsAdmin = 1,  // Orders & sellers management
    LogisticsAdmin = 2,   // Hubs, pickup, delivery
    FinanceAdmin = 3,     // Payments, settlements
    SupportAdmin = 4      // Customer support & disputes
}

/// <summary>
/// Comprehensive audit log for all platform actions
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public string UserRole { get; set; } = string.Empty;  // User, Seller, Admin
    public AdminRoleType? AdminSubRole { get; set; }      // If admin, which sub-role
    public string Action { get; set; } = string.Empty;    // CREATE, UPDATE, DELETE, LOGIN, APPROVE, REJECT, etc.
    public string EntityType { get; set; } = string.Empty; // Order, Product, User, Seller, etc.
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }  // JSON of previous values
    public string? NewValues { get; set; }  // JSON of new values
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string? AdditionalData { get; set; }  // Extra context as JSON
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
