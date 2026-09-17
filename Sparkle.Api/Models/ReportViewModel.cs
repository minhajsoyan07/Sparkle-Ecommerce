using System.ComponentModel.DataAnnotations;

namespace Sparkle.Api.Models;

public class ReportViewModel
{
    public string TargetType { get; set; } = "Product";
    public int TargetId { get; set; }

    [Required]
    public string Reason { get; set; } = string.Empty;

    [Required]
    [MinLength(10, ErrorMessage = "Please provide a detailed description (min 10 characters).")]
    public string Description { get; set; } = string.Empty;
}
