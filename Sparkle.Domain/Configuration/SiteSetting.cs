using Sparkle.Domain.Common;

namespace Sparkle.Domain.Configuration;

public class SiteSetting : BaseEntity
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string DataType { get; set; } = "string"; // string, int, decimal, bool, json
    public string Group { get; set; } = "General"; // General, Payment, Shipping, Email, System
    public string? Description { get; set; }
    public bool IsSystem { get; set; } // If true, cannot be deleted via UI
}
