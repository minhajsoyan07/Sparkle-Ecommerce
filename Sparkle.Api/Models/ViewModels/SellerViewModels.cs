using System.ComponentModel.DataAnnotations;
using Sparkle.Api.Attributes;

namespace Sparkle.Api.Models.ViewModels;

public class RegisterSellerViewModel
{
    [Required(ErrorMessage = "Full Name is required")]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Business Name is required")]
    [Display(Name = "Business Name")]
    public string BusinessName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category is required")]
    [Display(Name = "Business Category")]
    public string BusinessCategory { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone Number is required")]
    [Display(Name = "Contact Phone")]
    [BangladeshPhone]
    public string ContactPhone { get; set; } = string.Empty;

    [Display(Name = "Trade License #")]
    public string? BusinessRegistrationNumber { get; set; }

    [Display(Name = "Website / Page")]
    public string? BusinessWebsite { get; set; }

    [Display(Name = "Business Address")]
    public string? Address { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirm Password is required")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "The Password and Confirm Password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    // Legacy properties for backwards compatibility
    public string ShopName => BusinessName;
    public string PhoneNumber => ContactPhone;
    public string? Description => null;
}
