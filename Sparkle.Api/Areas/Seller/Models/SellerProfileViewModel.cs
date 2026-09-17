using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Sparkle.Api.Areas.Seller.Models;

public class SellerProfileViewModel
{
    // User Information
    [Display(Name = "Full Name")]
    public string? FullName { get; set; }

    [Display(Name = "Email Address")]
    public string? Email { get; set; } // Read-only mostly

    [Display(Name = "Personal Phone")]
    public string? PhoneNumber { get; set; }

    public string? ProfilePhotoPath { get; set; }

    [Display(Name = "Profile Photo")]
    public IFormFile? ProfilePhoto { get; set; }

    [Display(Name = "Gender")]
    public string? Gender { get; set; }

    // Shop Information
    [Required(ErrorMessage = "Shop Name is required")]
    [Display(Name = "Shop Name")]
    public string ShopName { get; set; } = string.Empty;

    [Display(Name = "Shop Description")]
    public string? ShopDescription { get; set; }

    [Required(ErrorMessage = "Mobile Number is required")]
    [Display(Name = "Business Mobile Number")]
    public string? MobileNumber { get; set; }

    [Display(Name = "Business Address")]
    public string? BusinessAddress { get; set; }

    [Display(Name = "City")]
    public string? City { get; set; }

    [Display(Name = "District")]
    public string? District { get; set; }
}
