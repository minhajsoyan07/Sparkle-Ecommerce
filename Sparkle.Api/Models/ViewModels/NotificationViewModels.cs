using System;
using System.ComponentModel.DataAnnotations;

namespace Sparkle.Api.Models.ViewModels;

/// <summary>
/// View model for notification items
/// </summary>
public class NotificationItem
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Type { get; set; } = "";

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = "";

    public DateTime CreatedAt { get; set; }
    
    public bool IsRead { get; set; }
}

/// <summary>
/// View model for transaction history items
/// </summary>
public class TransactionItem
{
    [Required]
    public string Id { get; set; } = "";

    public DateTime Date { get; set; }

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = "";

    [Required]
    [StringLength(50)]
    public string Type { get; set; } = "";

    [Range(0, double.MaxValue)]
    public decimal Amount { get; set; }

    public bool IsCredit { get; set; }

    [Required]
    [StringLength(50)]
    public string Status { get; set; } = "";
}
