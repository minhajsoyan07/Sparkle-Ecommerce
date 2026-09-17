using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sparkle.Domain.Orders;

namespace Sparkle.Api.Models.ViewModels;

/// <summary>
/// View model for displaying user profile information
/// </summary>
public class ProfileViewModel
{
    [Required]
    [StringLength(100)]
    public string FullName { get; set; } = "";

    [Required]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Phone]
    public string? ContactPhone { get; set; }

    [StringLength(500)]
    public string? Address { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public string? ProfilePhotoPath { get; set; }

    // Dashboard stats
    public List<Order> RecentOrders { get; set; } = new();
    public int TotalOrders { get; set; }
    public decimal TotalSpent { get; set; }
}

/// <summary>
/// View model for security settings
/// </summary>
public class SecurityViewModel
{
    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    public bool HasPassword { get; set; }
    public bool TwoFactorEnabled { get; set; }
}

/// <summary>
/// View model for password change
/// </summary>
public class ChangePasswordViewModel
{
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = "";

    [Required]
    [DataType(DataType.Password)]
    [Compare("NewPassword")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = "";
}
