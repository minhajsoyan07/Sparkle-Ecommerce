namespace Sparkle.Api.Models.Auth;

public class RegisterRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string? FullName { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public string UserId { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string[] Roles { get; set; } = Array.Empty<string>();
}
