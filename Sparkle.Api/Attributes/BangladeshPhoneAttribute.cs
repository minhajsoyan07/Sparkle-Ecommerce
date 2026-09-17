using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Sparkle.Api.Attributes;

/// <summary>
/// Validates Bangladesh phone numbers (11 digits, starting with 01)
/// Accepts formats: 01XXXXXXXXX, +8801XXXXXXXXX, 8801XXXXXXXXX
/// </summary>
public class BangladeshPhoneAttribute : ValidationAttribute
{
    public BangladeshPhoneAttribute()
    {
        ErrorMessage = "Invalid phone (use 01XXXXXXXXX)";
    }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
        {
            // Allow null/empty if field is not required
            return ValidationResult.Success;
        }

        var phoneNumber = value.ToString()!;
        
        // Remove all spaces, dashes, and parentheses
        phoneNumber = Regex.Replace(phoneNumber, @"[\s\-\(\)]", "");
        
        // Remove country code variations (+880, 880)
        if (phoneNumber.StartsWith("+880"))
        {
            phoneNumber = phoneNumber.Substring(4);
        }
        else if (phoneNumber.StartsWith("880"))
        {
            phoneNumber = phoneNumber.Substring(3);
        }
        
        // Now check if it matches BD phone pattern: 01XXXXXXXXX (11 digits starting with 01)
        if (!Regex.IsMatch(phoneNumber, @"^01[0-9]{9}$"))
        {
            return new ValidationResult(ErrorMessage ?? "Invalid phone (use 01XXXXXXXXX)");
        }
        
        return ValidationResult.Success;
    }
}
