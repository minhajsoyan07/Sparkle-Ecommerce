namespace Sparkle.Domain.Identity;

/// <summary>
/// Stores user preferences for AI-powered search features
/// </summary>
public class UserSearchPreference
{
    public int Id { get; set; }
    public string UserId { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;
    
    // Smart Search Settings
    public bool IntelligentSearchEnabled { get; set; } = true;
    public bool ShowPersonalization { get; set; } = true;
    public bool AutoApplyFilters { get; set; } = true;
    public bool ShowConfidenceScores { get; set; } = true;
    
    // Audit
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
