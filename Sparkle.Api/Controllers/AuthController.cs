using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Sparkle.Domain.Identity;
using Sparkle.Domain.Orders;
using Sparkle.Infrastructure;
using Sparkle.Domain.Sellers;
using Sparkle.Api.Attributes;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Models.ViewModels;
namespace Sparkle.Api.Controllers;

[Route("auth")]
public class AuthController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<AuthController> _logger;
    private readonly ApplicationDbContext _db;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IWebHostEnvironment environment,
        ILogger<AuthController> logger,
        ApplicationDbContext db)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _environment = environment;
        _logger = logger;
        _db = db;
    }

    [HttpGet("register-seller")]
    [AllowAnonymous]
    public IActionResult RegisterSeller()
    {
        // Prevent redirect loop: Only redirect if user has Role AND Entity
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Seller"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var hasSellerEntity = _db.Sellers.Any(s => s.UserId == userId);
            
            if (hasSellerEntity)
            {
                return RedirectToAction("Index", "Dashboard", new { area = "Seller" });
            }
        }
        return View(new RegisterSellerViewModel());
    }

    [HttpPost("register-seller")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterSeller(RegisterSellerViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        // Check for duplicate email
        var existingEmail = await _userManager.FindByEmailAsync(model.Email);
        if (existingEmail != null)
        {
            ModelState.AddModelError(nameof(model.Email), "Email already registered");
            return View(model);
        }

        // Normalize and check for duplicate phone number
        var normalizedPhone = model.ContactPhone?.Trim();
        if (!string.IsNullOrEmpty(normalizedPhone))
        {
            // Remove country code for consistent duplicate check
            if (normalizedPhone.StartsWith("+880")) normalizedPhone = normalizedPhone.Substring(4);
            else if (normalizedPhone.StartsWith("880")) normalizedPhone = normalizedPhone.Substring(3);
            
            // Ensure it starts with 0 for storage
            if (!normalizedPhone.StartsWith("0")) normalizedPhone = "0" + normalizedPhone;
            
            var existingPhone = await _userManager.Users.AnyAsync(u => u.PhoneNumber == normalizedPhone || u.PhoneNumber == model.ContactPhone);
            if (existingPhone)
            {
                ModelState.AddModelError(nameof(model.ContactPhone), "Phone already registered");
                return View(model);
            }
        }

        var user = new ApplicationUser 
        { 
            UserName = model.Email, 
            Email = model.Email,
            FullName = model.FullName, 
            PhoneNumber = normalizedPhone ?? model.ContactPhone,
            EmailConfirmed = true,
            IsActive = true,
            RegisteredAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded)
        {
            await _userManager.AddToRoleAsync(user, "Seller");
            
            // Create Seller entity
                // Create Seller entity
                string description = $"Category: {model.BusinessCategory}";
                if (!string.IsNullOrEmpty(model.BusinessWebsite))
                {
                    description += $" | Website: {model.BusinessWebsite}";
                }

                // Parse Location from Address (Format: Division, District, Thana, Area | Street)
                string? city = null;
                string? district = null;
                if (!string.IsNullOrEmpty(model.Address))
                {
                    var parts = model.Address.Split(',');
                    if (parts.Length > 0) city = parts[0].Trim();
                    if (parts.Length > 1) district = parts[1].Trim();
                }

                var seller = new Seller
                {
                    UserId = user.Id,
                    ShopName = model.BusinessName,
                    ShopDescription = description,
                    MobileNumber = normalizedPhone ?? model.ContactPhone,
                    BusinessAddress = model.Address,
                    City = city,
                    District = district,
                    NidNumber = model.BusinessRegistrationNumber, // Mapping Trade License to NID field
                    Status = SellerStatus.Pending
                };
            _db.Sellers.Add(seller);
            await _db.SaveChangesAsync();

            // Do NOT sign in the seller automatically.
            // await _signInManager.SignInAsync(user, isPersistent: false);
            
            // Redirect to a confirmation page
            return RedirectToAction("RegistrationPending", "Auth");
        }

        foreach (var error in result.Errors)
        {
            // Provide user-friendly error messages
            if (error.Code == "DuplicateEmail" || error.Code == "DuplicateUserName")
            {
                ModelState.AddModelError(nameof(model.Email), "Email already registered");
            }
            else if (error.Code == "PasswordTooShort")
            {
                ModelState.AddModelError(nameof(model.Password), "Minimum 6 characters required");
            }
            else if (error.Code == "PasswordRequiresNonAlphanumeric")
            {
                ModelState.AddModelError(nameof(model.Password), "Include a special character");
            }
            else if (error.Code == "PasswordRequiresDigit")
            {
                ModelState.AddModelError(nameof(model.Password), "Include a number");
            }
            else if (error.Code == "PasswordRequiresUpper")
            {
                ModelState.AddModelError(nameof(model.Password), "Include an uppercase letter");
            }
            else
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        return View(model);
    }

    [HttpGet("login")]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, string loginType = "user")
    {
        _logger.LogInformation($"[Login] Attempt for {model.Email} with type: {loginType}");

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("[Login] ModelState invalid.");
            return View(model);
        }

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
             _logger.LogWarning("[Login] User not found.");
             ModelState.AddModelError(string.Empty, "Invalid email or password. Please check your credentials and try again.");
             return View(model);
        }
        
        if (!user.IsActive)
        {
             _logger.LogWarning("[Login] User inactive.");
             ModelState.AddModelError(string.Empty, "Your account is inactive. Please contact support for assistance.");
             return View(model);
        }

        // Check if login type matches user's actual role
        var isSeller = await _userManager.IsInRoleAsync(user, "Seller");
        var isUser = await _userManager.IsInRoleAsync(user, "User");
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");

        _logger.LogInformation($"[Login] Roles - Seller: {isSeller}, User: {isUser}, Admin: {isAdmin}");

        // Self-healing: If user has a Seller entity but no role, fix it
        if (loginType == "seller" && !isSeller)
        {
             _logger.LogInformation("[Login] Self-healing Seller role...");
             var sellerEntity = _db.Sellers.FirstOrDefault(s => s.UserId == user.Id);
             if (sellerEntity != null)
             {
                 await _userManager.AddToRoleAsync(user, "Seller");
                 isSeller = true;
                 _logger.LogInformation("[Login] Self-healing SUCCESS.");
             }
             else
             {
                 _logger.LogWarning("[Login] Self-healing FAILED - No Seller entity found.");
             }
        }

        // CHECK SELLER STATUS
        if (isSeller && loginType == "seller")
        {
            var sellerEntity = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == user.Id);
            if (sellerEntity != null)
            {
                if (sellerEntity.Status == SellerStatus.Pending || sellerEntity.Status == SellerStatus.Isolated)
                {
                    ModelState.AddModelError(string.Empty, "Your application is under review. Please wait for admin approval.");
                    return View(model);
                }
                if (sellerEntity.Status == SellerStatus.Rejected)
                {
                    ModelState.AddModelError(string.Empty, "This email has been reset by the admin. Please try registering with another email.");
                    return View(model);
                }
                // Only allow if Approved
                if (sellerEntity.Status != SellerStatus.Approved)
                {
                    ModelState.AddModelError(string.Empty, "Access denied.");
                    return View(model);
                }
            }
        }

        // Validate login type matches account type
        if (loginType == "seller" && !isSeller)
        {
            _logger.LogWarning("[Login] Rejected: LoginType is Seller but user is not.");
            ModelState.AddModelError(string.Empty, "You are trying to log in with a User account. Please use the User Login panel.");
            return View(model);
        }

        if (loginType == "user" && !isUser)
        {
            if (isSeller)
            {
                 _logger.LogWarning("[Login] Rejected: Seller trying to login as User.");
                ModelState.AddModelError(string.Empty, "You are trying to log in with a Seller account. Please use the Seller Login panel.");
            }
            else if (isAdmin)
            {
                ModelState.AddModelError(string.Empty, "Admin accounts must use the admin login page.");
            }
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
        {
            _logger.LogWarning($"[Login] PasswordSignInAsync failed. Result: {result}");
            ModelState.AddModelError(string.Empty, "Invalid email or password. Please check your credentials and try again.");
            return View(model);
        }

        _logger.LogInformation("[Login] PasswordSignInAsync SUCCESS.");

        // Merge Guest Cart if exists
        await MergeGuestCart(user.Id);

        // STRICT ROLE-BASED REDIRECT - Role dashboard takes priority
        // Only allow ReturnUrl if it's within the same role area
        
        // Admin users should use admin-login page
        if (isAdmin)
        {
            _logger.LogInformation("[Login] Admin user detected - redirecting to admin login.");
            await _signInManager.SignOutAsync();
            return Redirect("/auth/admin-login");
        }
        
        // Seller redirect
        if (isSeller)
        {
            _logger.LogInformation("[Login] Redirecting to Seller Dashboard.");
            // Only allow ReturnUrl if it starts with /Seller
            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) && model.ReturnUrl.StartsWith("/Seller", StringComparison.OrdinalIgnoreCase))
            {
                return Redirect(model.ReturnUrl);
            }
            return Redirect("/Seller/Dashboard");
        }

        // Regular User redirect
        _logger.LogInformation("[Login] Redirecting to User Home.");
        // Only allow ReturnUrl if it does NOT start with /Seller or /Admin
        if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl) 
            && !model.ReturnUrl.StartsWith("/Seller", StringComparison.OrdinalIgnoreCase)
            && !model.ReturnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Redirect(model.ReturnUrl);
        }
        return Redirect("/"); // Redirect to Homepage (Product Dashboard)
    }

    [HttpGet("admin-login")]
    [HttpGet("/admin/login")] // Alternative route: /admin/login
    [AllowAnonymous]
    public IActionResult AdminLogin(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("admin-login")]
    [HttpPost("/admin/login")] // Alternative route: /admin/login
    [AllowAnonymous]
    public async Task<IActionResult> AdminLogin(LoginViewModel model)
    {
        if (model == null)
        {
            return View(new LoginViewModel());
        }

        _logger.LogInformation("========== ADMIN LOGIN ATTEMPT ==========");
        _logger.LogInformation($"Email: {model?.Email ?? "NULL"}");
        _logger.LogInformation($"Password Length: {model?.Password?.Length ?? 0}");
        _logger.LogInformation($"RememberMe: {model?.RememberMe ?? false}");
        _logger.LogInformation($"ModelState.IsValid: {ModelState.IsValid}");

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("ModelState is INVALID. Errors:");
            foreach (var key in ModelState.Keys)
            {
                var errors = ModelState[key]?.Errors;
                if (errors != null && errors.Count > 0)
                {
                    foreach (var error in errors)
                    {
                        _logger.LogWarning($"  [{key}]: {error.ErrorMessage}");
                    }
                }
            }
            return View(model);
        }

        _logger.LogInformation("ModelState is valid. Looking up user...");
        var user = await _userManager.FindByEmailAsync(model?.Email ?? "");
        
        _logger.LogInformation($"User found: {user != null}");
        if (user == null)
        {
            _logger.LogWarning($"No user found with email: {model?.Email ?? "unknown"}");
            ModelState.AddModelError(string.Empty, "Invalid admin email or password. Please check your credentials.");
            return View(model);
        }

        _logger.LogInformation($"User ID: {user.Id}");
        _logger.LogInformation($"User.IsActive: {user.IsActive}");
        
        if (!user.IsActive)
        {
            _logger.LogWarning($"User account is INACTIVE: {model?.Email ?? "unknown"}");
            ModelState.AddModelError(string.Empty, "Your admin account is inactive. Please contact the system administrator.");
            return View(model);
        }

        // Check if user is admin
        _logger.LogInformation("Checking if user has Admin role...");
        var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
        _logger.LogInformation($"Is Admin: {isAdmin}");
        
        // Self-healing: Ensure admin@sparkle.local always has Admin role
        if (user.Email == "admin@sparkle.local" && !isAdmin)
        {
            _logger.LogWarning("Self-healing: Assigning Admin role to admin@sparkle.local");
            await _userManager.AddToRoleAsync(user, "Admin");
            isAdmin = true;
        }

        if (!isAdmin)
        {
            _logger.LogWarning($"User {model?.Email ?? "unknown"} is NOT an admin");
            ModelState.AddModelError(string.Empty, "Access denied. Admin credentials required.");
            return View(model);
        }

        _logger.LogInformation("Attempting password sign-in...");
        var result = await _signInManager.PasswordSignInAsync(user, model?.Password ?? string.Empty, model?.RememberMe ?? false, lockoutOnFailure: false);
        
        _logger.LogInformation($"Sign-in result - Succeeded: {result.Succeeded}");
        _logger.LogInformation($"Sign-in result - IsLockedOut: {result.IsLockedOut}");
        _logger.LogInformation($"Sign-in result - IsNotAllowed: {result.IsNotAllowed}");
        _logger.LogInformation($"Sign-in result - RequiresTwoFactor: {result.RequiresTwoFactor}");
        
        if (!result.Succeeded)
        {


            
            _logger.LogError($"Password sign-in FAILED for user: {model?.Email ?? "unknown"}");
            ModelState.AddModelError(string.Empty, "Invalid admin email or password. Please check your credentials.");
            return View(model);
        }

        _logger.LogInformation("Password sign-in SUCCEEDED!");
        
        // STRICT ROLE-BASED REDIRECT - Only allow ReturnUrl within Admin area
        if (!string.IsNullOrEmpty(model?.ReturnUrl) && Url.IsLocalUrl(model?.ReturnUrl) 
            && model.ReturnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation($"Redirecting to Admin ReturnUrl: {model.ReturnUrl}");
            return Redirect(model.ReturnUrl);
        }

        _logger.LogInformation("Redirecting to /Admin/Dashboard");
        return Redirect("/Admin/Dashboard");
    }

    [HttpPost("external-login")]
    [AllowAnonymous]
    public IActionResult ExternalLogin(string provider, string? returnUrl = null)
    {
        // Check if the provider is supported and configured
        if (provider == "Google")
        {
            var googleConfig = HttpContext.RequestServices.GetService<Services.IGoogleAuthConfigurationService>();
            if (googleConfig?.IsGoogleAuthConfigured != true)
            {
                ModelState.AddModelError(string.Empty, "Google login is currently not configured. Please contact the administrator or use email/password login.");
                return RedirectToAction(nameof(Login));
            }
        }

        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Challenge(properties, provider);
    }

    [HttpGet("external-login-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> ExternalLoginCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (remoteError != null)
        {
            _logger.LogWarning($"Error from external provider: {remoteError}");
            ModelState.AddModelError(string.Empty, $"Error from external provider: {remoteError}");
            return RedirectToAction(nameof(Login));
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogError("External login info was null. This usually means the user cancelled the flow, or the Google Client ID/Secret is mismatched/invalid.");
            ModelState.AddModelError(string.Empty, "Failed to retrieve information from the external login provider. Please try again or use email/password login.");
            return RedirectToAction(nameof(Login));
        }

        _logger.LogInformation($"External login attempt from provider: {info.LoginProvider}");
        _logger.LogInformation($"Provider Key: {info.ProviderKey}");

        // Sign in the user with this external login provider if the user already has a login
        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        
        if (result.Succeeded)
        {
            _logger.LogInformation($"User successfully logged in via {info.LoginProvider}");
            return LocalRedirect(returnUrl ?? "/");
        }
        
        if (result.IsLockedOut)
        {
             _logger.LogWarning($"User account is locked out for {info.LoginProvider}");
             return RedirectToAction(nameof(Login));
        }

        // If the user does not have an account, create one
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var name = info.Principal.FindFirstValue(ClaimTypes.Name);
        _logger.LogInformation($"New external login user detected. Email: {email}, Name: {name}");

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning($"Email not received from {info.LoginProvider}");
            ModelState.AddModelError(string.Empty, $"Email not received from {info.LoginProvider}. Please ensure you've granted email permission.");
            return RedirectToAction(nameof(Login));
        }

        var user = await _userManager.FindByEmailAsync(email);
        if (user == null)
        {
            // Create new user
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FullName = info.Principal.FindFirstValue(ClaimTypes.Name) ?? email.Split('@')[0],
                IsActive = true,
                ContactPhone = "",
                Address = "",
                DateOfBirth = DateTime.Now.AddYears(-20) // Default age
            };

            var createResult = await _userManager.CreateAsync(user);
            if (!createResult.Succeeded)
            {
                _logger.LogError($"Failed to create user account for {email}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                ModelState.AddModelError(string.Empty, "Failed to create user account. Please contact support.");
                return RedirectToAction(nameof(Login));
            }

            // Assign User role (Google login is for users only, not vendors or admins)
            await _userManager.AddToRoleAsync(user, "User");
            _logger.LogInformation($"Created new user account for {email} via {info.LoginProvider}");
        }

        // Add external login to user
        var addLoginResult = await _userManager.AddLoginAsync(user, info);
        if (addLoginResult.Succeeded)
        {
            await _signInManager.SignInAsync(user, isPersistent: false);
            _logger.LogInformation($"Successfully linked {info.LoginProvider} to user {email}");
            return LocalRedirect(returnUrl ?? "/");
        }

        _logger.LogError($"Failed to link external login for {email}: {string.Join(", ", addLoginResult.Errors.Select(e => e.Description))}");
        ModelState.AddModelError(string.Empty, "Failed to link external login. Please try again or contact support.");
        return RedirectToAction(nameof(Login));
    }

    [HttpGet("register")]
    [AllowAnonymous]
    public IActionResult RegisterUser()
    {
        return View(new RegisterUserViewModel());
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterUser(RegisterUserViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Handle NID file uploads (optional)
        string? nidFrontPath = null;
        string? nidBackPath = null;

        if (model.NationalIdFront?.Length > 0)
        {
            nidFrontPath = await SaveNidFileAsync(model.NationalIdFront, "front");
        }

        if (model.NationalIdBack?.Length > 0)
        {
            nidBackPath = await SaveNidFileAsync(model.NationalIdBack, "back");
        }

        var composedAddress = string.IsNullOrWhiteSpace(model.Location)
            ? (model.Address ?? "")
            : $"{model.Location} | {model.Address ?? ""}";

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            FullName = model.FullName,
            IsActive = true,
            ContactPhone = model.ContactPhone,
            Address = string.IsNullOrWhiteSpace(composedAddress) ? "" : composedAddress,
            DateOfBirth = model.DateOfBirth,
            NationalIdFrontPath = nidFrontPath,
            NationalIdBackPath = nidBackPath
        };

        // Check for duplicate phone number
        if (await _userManager.Users.AnyAsync(u => u.PhoneNumber == model.ContactPhone))
        {
            ModelState.AddModelError(nameof(model.ContactPhone), "This phone number is already registered.");
            return View(model);
        }

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                if (error.Code == "DuplicateEmail" || error.Code == "DuplicateUserName")
                {
                    ModelState.AddModelError(nameof(model.Email), "This email address is already registered.");
                }
                else if (error.Code == "PasswordTooShort")
                {
                    ModelState.AddModelError(nameof(model.Password), "Minimum 6 characters required");
                }
                else if (error.Code == "PasswordRequiresDigit")
                {
                    ModelState.AddModelError(nameof(model.Password), "Include at least one number");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }
            return View(model);
        }

        await EnsureRoleExists("User");
        await _userManager.AddToRoleAsync(user, "User");

        await _signInManager.SignInAsync(user, isPersistent: true);
        return Redirect("/");
    }



    [HttpGet("logout")]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    private async Task EnsureRoleExists(string roleName)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            await _roleManager.CreateAsync(new ApplicationRole { Name = roleName });
        }
    }

    private async Task<string> SaveNidFileAsync(IFormFile file, string side)
    {
        var uploadsRoot = Path.Combine(_environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"), "uploads", "nid");
        Directory.CreateDirectory(uploadsRoot);

        var safeFileName = Path.GetFileNameWithoutExtension(file.FileName);
        var extension = Path.GetExtension(file.FileName);
        var uniqueName = $"nid_{side}_{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsRoot, uniqueName);

        using (var stream = System.IO.File.Create(filePath))
        {
            await file.CopyToAsync(stream);
        }

        // Return relative path for storing in database
        var relativePath = $"/uploads/nid/{uniqueName}";
        return relativePath;
    }

    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
        public string? ReturnUrl { get; set; }
    }

    public class RegisterUserViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm Password is required")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;

        // Extra information collected during signup
        [Required(ErrorMessage = "Mobile is required")]
        [BangladeshPhone]
        public string ContactPhone { get; set; } = string.Empty;

        [Display(Name = "Address")]
        public string? Address { get; set; }

        [Display(Name = "City / Division")]
        public string? Location { get; set; }

        [Required(ErrorMessage = "Date of Birth is required")]
        [Display(Name = "Date of Birth")]
        [DataType(DataType.Date)]
        public DateTime? DateOfBirth { get; set; }

        [Display(Name = "Upload NID (Front)")]
        public IFormFile? NationalIdFront { get; set; }

        [Display(Name = "Upload NID (Back)")]
        public IFormFile? NationalIdBack { get; set; }
    }

    public class RegisterSellerViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email format")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Business Name is required")]
        public string BusinessName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Category is required")]
        public string BusinessCategory { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone Number is required")]
        [BangladeshPhone]
        public string ContactPhone { get; set; } = string.Empty;

        public string? BusinessRegistrationNumber { get; set; }

        public string? BusinessWebsite { get; set; }

        public string? Address { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Confirm Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm Password")]
        [Compare("Password", ErrorMessage = "The Password and Confirm Password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }


    [HttpGet("registration-pending")]
    [AllowAnonymous]
    public IActionResult RegistrationPending()
    {
        return View();
    }

    private async Task MergeGuestCart(string userId)
    {
        // 1. Get Guest Cart ID from Cookie
        if (!Request.Cookies.TryGetValue("Sparkle_GuestCartId", out var guestId) || string.IsNullOrEmpty(guestId))
        {
            return;
        }

        // 2. Find Guest Cart in DB
        var guestCart = await _db.Carts
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == guestId);

        if (guestCart != null && guestCart.Items.Any())
        {
            // 3. Find/Create User Cart
            var userCart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (userCart == null)
            {
                userCart = new Cart { UserId = userId };
                _db.Carts.Add(userCart);
            }

            // 4. Merge Items
            foreach (var item in guestCart.Items)
            {
                var existing = userCart.Items.FirstOrDefault(i => i.ProductVariantId == item.ProductVariantId);
                if (existing != null)
                {
                    existing.Quantity += item.Quantity;
                }
                else
                {
                     // Must create new object, cannot move entity
                     userCart.Items.Add(new CartItem
                     {
                         ProductVariantId = item.ProductVariantId,
                         Quantity = item.Quantity,
                         UnitPrice = item.UnitPrice
                     });
                }
            }

            // 5. Cleanup Guest Cart
            _db.Carts.Remove(guestCart); // Cascade delete should handle items
            
            await _db.SaveChangesAsync();
        }

        // 6. Delete Cookie
        Response.Cookies.Delete("Sparkle_GuestCartId");
    }
}
