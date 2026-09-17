using Sparkle.Domain.Common;

namespace Sparkle.Domain.Content;

public class StaticPage : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty; // HTML content
    
    public string? MetaTitle { get; set; }
    public string? MetaDescription { get; set; }
    
    public bool IsPublished { get; set; } = true;
    public int DisplayOrder { get; set; }
    
    public string Location { get; set; } = "Footer"; // Header, Footer, Sidebar
}

public class FaqItem : BaseEntity
{
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    
    public string Category { get; set; } = "General";
    public int DisplayOrder { get; set; }
    public bool IsPublished { get; set; } = true;
}
