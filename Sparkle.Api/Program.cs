using System.Text;
using System.Linq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Sellers;
using Sparkle.Infrastructure;
using Sparkle.Domain.Identity;
using Sparkle.Infrastructure.Services;
using Sparkle.Api.Services;
using Sparkle.Domain.Configuration;
using StackExchange.Redis;
var builder = WebApplication.CreateBuilder(args); // Trigger Restart 2

// Database & Identity
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options => 
    options.UseSqlServer(connectionString, sqlOptions => {
        sqlOptions.CommandTimeout(180);
        sqlOptions.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null);
        sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
    }));

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        // Password settings
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false; // User didn't explicitly ask, but good practice. Kept false to strictly follow "num and word" minimal request, but example has Uppercase. Let's keep false to not be too annoying, enforcing Alphanumeric is key.
        options.Password.RequireNonAlphanumeric = false; // User didn't explicitly ask for symbols in text description, though example has it.
        options.Password.RequiredLength = 6;
        
        // Lockout settings
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        
        // User settings
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;
        options.SignIn.RequireConfirmedAccount = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// Google OAuth for Users Only
var googleClientId = builder.Configuration["Google:ClientId"];
var googleClientSecret = builder.Configuration["Google:ClientSecret"];
var isGoogleConfigured = !string.IsNullOrWhiteSpace(googleClientId) && 
                         !string.IsNullOrWhiteSpace(googleClientSecret) &&
                         googleClientId != "YOUR_GOOGLE_CLIENT_ID_HERE" &&
                         googleClientSecret != "YOUR_GOOGLE_CLIENT_SECRET_HERE";

if (isGoogleConfigured)
{
    builder.Services.AddAuthentication()
        .AddGoogle(options =>
        {
            options.ClientId = googleClientId!;
            options.ClientSecret = googleClientSecret!;
            options.SaveTokens = true;
        });
    Console.WriteLine($"[OK] Google OAuth authentication is enabled. ClientId: {googleClientId?[..5]}... (Redacted)");
    Console.WriteLine("[INFO] Verifying SQL Server connection...");
}
else
{
    Console.WriteLine("[INFO] Google OAuth is NOT configured. Google login will be disabled.");
    Console.WriteLine("[INFO] To enable Google login, see GOOGLE_OAUTH_SETUP_GUIDE.md in the project root.");
}

// Store configuration status for use in views
builder.Services.AddSingleton<Sparkle.Api.Services.IGoogleAuthConfigurationService>(sp => 
    new Sparkle.Api.Services.GoogleAuthConfigurationService(isGoogleConfigured));


// Configure cookie authentication
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/auth/login";
    options.LogoutPath = "/auth/logout";
    options.AccessDeniedPath = "/auth/access-denied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

// JWT Authentication for APIs (cookies remain default for MVC)
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("JWT Key is missing");
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication()
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSection["Issuer"],
            ValidAudience = jwtSection["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes)
        };
    });

// Application Services
builder.Services.AddScoped<Sparkle.Infrastructure.Services.ICommissionService, Sparkle.Infrastructure.Services.CommissionService>();
builder.Services.AddScoped<Sparkle.Domain.Marketing.ILoyaltyService, Sparkle.Infrastructure.Services.LoyaltyService>();
builder.Services.AddScoped<Sparkle.Domain.Configuration.ISettingsService, Sparkle.Api.Services.SettingsService>();
builder.Services.AddTransient<Sparkle.Infrastructure.Services.LocationSeedingService>();
builder.Services.AddScoped<Sparkle.Api.Services.IDynamicFormService, Sparkle.Api.Services.DynamicFormService>();
builder.Services.AddScoped<Sparkle.Api.Services.DynamicFormSeedingService>();
builder.Services.AddScoped<Sparkle.Api.Services.IProductService, Sparkle.Api.Services.ProductService>();
builder.Services.AddScoped<Sparkle.Infrastructure.Services.ShipmentService>();
builder.Services.AddScoped<Sparkle.Api.Services.IReviewService, Sparkle.Api.Services.ReviewService>();
builder.Services.AddScoped<Sparkle.Api.Services.IEmailService, Sparkle.Api.Services.EmailService>();
builder.Services.AddScoped<Sparkle.Api.Services.ICachingService, Sparkle.Api.Services.RedisCachingService>();
builder.Services.AddScoped<Sparkle.Api.Services.ISellerPerformanceService, Sparkle.Api.Services.SellerPerformanceService>();
builder.Services.AddScoped<Sparkle.Api.Services.IInvoiceService, Sparkle.Api.Services.InvoiceService>();
builder.Services.AddScoped<Sparkle.Infrastructure.Services.IAIService, Sparkle.Infrastructure.Services.AIService>();
builder.Services.AddScoped<Sparkle.Api.Services.IStockManagementService, Sparkle.Api.Services.StockManagementService>();

// Homepage Sections & Intelligent Analysis Services
builder.Services.AddScoped<Sparkle.Api.Services.IHomepageSectionService, Sparkle.Api.Services.HomepageSectionService>();
builder.Services.AddScoped<Sparkle.Api.Services.IIntelligentProductAnalysisService, Sparkle.Api.Services.IntelligentProductAnalysisService>();
builder.Services.AddScoped<Sparkle.Api.Services.IAIRecommendationService, Sparkle.Api.Services.AIRecommendationService>();
builder.Services.AddScoped<Sparkle.Api.Services.HomepageSectionSeedingService>();
builder.Services.AddScoped<Sparkle.Api.Services.IPdfReportService, Sparkle.Api.Services.PdfReportService>();

// Category Image & Icon Service
builder.Services.AddScoped<Sparkle.Api.Services.ICategoryImageService, Sparkle.Api.Services.CategoryImageService>();

// Seller Authorization & Stock Management
builder.Services.AddScoped<Sparkle.Api.Services.ISellerAuthorizationService, Sparkle.Api.Services.SellerAuthorizationService>();

// New Platform Services from Package
builder.Services.AddScoped<Sparkle.Domain.Intelligence.ISmartSearchService, Sparkle.Infrastructure.Intelligence.SmartSearchService>();
builder.Services.AddScoped<Sparkle.Infrastructure.Services.IWalletService, Sparkle.Infrastructure.Services.WalletService>();
builder.Services.AddScoped<Sparkle.Infrastructure.Services.ISupportService, Sparkle.Infrastructure.Services.SupportService>();
builder.Services.AddScoped<Sparkle.Infrastructure.Services.ILogisticsService, Sparkle.Infrastructure.Services.LogisticsService>();
builder.Services.AddScoped<Sparkle.Infrastructure.Services.INotificationService, Sparkle.Api.Services.NotificationService>();
builder.Services.AddHttpClient<Sparkle.Infrastructure.Services.IPaymentService, Sparkle.Infrastructure.Services.SSLCommerzService>();

// Order Cancellation Background Service
builder.Services.AddHostedService<Sparkle.Infrastructure.Services.OrderCancellationService>();

// Swagger/OpenAPI Documentation
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Sparkle API", Version = "v1" });
    // Use FullName to avoid conflicts between controllers with the same name in different Areas
    c.CustomSchemaIds(type => type.FullName);

    // Include all controllers in Swagger for testing
    // c.DocInclusionPredicate((docName, apiDesc) => true);
    
    // Resolve conflicting actions by taking the first one
    c.ResolveConflictingActions(apiDescriptions => apiDescriptions.First());

    // Add JWT Authentication support to Swagger
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. \r\n\r\n Enter 'Bearer' [space] and then your token in the text input below.\r\n\r\nExample: \"Bearer 1safsfsdfdfd\""
    });

    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
});

// SignalR with Redis Backplane (optional - falls back to in-memory if Redis unavailable)
builder.Services.AddSignalR();
// Session Support for Checkout
builder.Services.AddDistributedMemoryCache(); // Force Memory Cache for reliability
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(60);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".Sparkle.Session";
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Response Compression for better performance
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest;
});

// Response Caching
builder.Services.AddResponseCaching();

// Add services to the container.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddRazorPages();


var app = builder.Build();

// Seed default roles/admin user and Initialize DB
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var db = services.GetRequiredService<ApplicationDbContext>();

    try
    {
        // New Robust DB Initialization (Includes Schema Repair and Optimization)
        await Sparkle.Api.Data.DbInitializer.InitializeAsync(services);
        
        // Seed Homepage Sections
        var sectionSeeder = services.GetRequiredService<Sparkle.Api.Services.HomepageSectionSeedingService>();
        await sectionSeeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database initialization.");
    }


    try
    {
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        string[] roles = ["User", "Seller", "Admin"]; 
        foreach (var roleName in roles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = roleName });
            }
        }

        var adminEmail = "admin@sparkle.local";
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Super Admin"
            };

            var createResult = await userManager.CreateAsync(admin, "Admin@123");
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
            }
        }

        // Cleanup Guest Carts on Startup as requested
        try 
        {
            var result = await db.Database.ExecuteSqlRawAsync("DELETE FROM [orders].[Carts] WHERE [UserId] LIKE 'guest_%'");
            logger.LogInformation($"[Startup] Cleaned up {result} guest carts.");
        }
        catch (Exception ex)
        {
             logger.LogWarning(ex, "[Startup] Failed to cleanup guest carts.");
        }

        // Ensure chat DeletedFor column exists (for delete-for-me vs delete-for-everyone)
        try
        {
            await db.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('support.ChatMessages') AND name = 'DeletedFor')
                BEGIN
                    ALTER TABLE [support].[ChatMessages] ADD [DeletedFor] nvarchar(450) NULL;
                END");
            logger.LogInformation("[Startup] Chat DeletedFor column ensured.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Startup] Failed to ensure ChatMessages.DeletedFor column.");
        }

        // Seed comprehensive categories (24 categories for Bangladesh e-commerce)
        // Ensure each required category slug exists even if the table already has older data
        var requiredCategories = new List<Category>
        {
            new Category { Name = "Electronics & Gadgets", Slug = "electronics-gadgets" },
            new Category { Name = "Fashion & Lifestyle", Slug = "fashion-lifestyle" },
            new Category { Name = "Home & Living", Slug = "home-living" },
            new Category { Name = "Mobiles & Tablets", Slug = "mobiles-tablets" },
            new Category { Name = "Laptops & Computers", Slug = "laptops-computers" },
            new Category { Name = "Home Appliances", Slug = "home-appliances" },
            new Category { Name = "Kitchen Appliances", Slug = "kitchen-small-appliances" },
            new Category { Name = "Beauty & Personal Care", Slug = "beauty-personal-care" },
            new Category { Name = "Jewelry & Accessories", Slug = "jewelry-accessories" },
            new Category { Name = "Groceries & Essentials", Slug = "groceries-essentials" },
            new Category { Name = "Baby, Kids & Mom", Slug = "baby-kids-mom" },
            new Category { Name = "Toys & Games", Slug = "toys-games" },
            new Category { Name = "Books & Stationery", Slug = "books-stationery" },
            new Category { Name = "Sports & Outdoors", Slug = "sports-outdoors" },
            new Category { Name = "Automotive & Bike", Slug = "automotive-bike" },
            new Category { Name = "Pet Supplies", Slug = "pet-supplies" },
            new Category { Name = "Local BD Brands", Slug = "local-bd-brands" },
            new Category { Name = "Medicine & Wellness", Slug = "medicine-wellness" },
            new Category { Name = "Photography & Camera", Slug = "photography-camera" },
            new Category { Name = "Art & Crafts", Slug = "art-crafts" },
            new Category { Name = "Musical Instruments", Slug = "musical-instruments" },
            new Category { Name = "Garden & Outdoor", Slug = "garden-outdoor" },
            new Category { Name = "Travel & Luggage", Slug = "travel-luggage" },
            new Category { Name = "Fitness & Gym", Slug = "fitness-gym" }
        };

        var slugs = requiredCategories.Select(c => c.Slug).ToList();
        var existingCategories = await db.Categories
            .Where(c => slugs.Contains(c.Slug))
            .ToDictionaryAsync(c => c.Slug);

        foreach (var cat in requiredCategories)
        {
            if (!existingCategories.TryGetValue(cat.Slug, out var existing))
            {
                db.Categories.Add(cat);
            }
            else if (existing.Name != cat.Name)
            {
                existing.Name = cat.Name;
            }
        }

        if (db.ChangeTracker.HasChanges())
        {
            await db.SaveChangesAsync();
        }

        // Seed 25 comprehensive sellers with realistic Bangladesh business data
        if (!await db.Sellers.AnyAsync())
        {
            var sellerConfigs = new[]
            {
                // Original 10 sellers
                new { Email = "dailyessentials@sparkle.local", Password = "Vendor@123", FullName = "Abdul Malek", ShopName = "Daily Essentials BD", ShopDescription = "Your one-stop shop for daily household needs, electronics, and kitchenware. Quality products at affordable prices.", City = "Dhaka", District = "Dhaka", Phone = "+880 1711-223344", Bkash = "01711223344", Rating = 4.8m, Address = "Plot-5, Road-12, Section-10, Mirpur, Dhaka" },
                new { Email = "dailymart@sparkle.local", Password = "Vendor@123", FullName = "Salma Begum", ShopName = "DailyMart BD", ShopDescription = "Everything you need for your daily life. Groceries, home appliances, and household essentials delivered to your door.", City = "Chittagong", District = "Chittagong", Phone = "+880 1822-334455", Bkash = "01822334455", Rating = 4.7m, Address = "12, Nellie Road, Chittagong-4000" },
                new { Email = "homeneeds@sparkle.local", Password = "Vendor@123", FullName = "Rafiq Ahmed", ShopName = "HomeNeeds BD", ShopDescription = "Quality home essentials, electronics, and personal care products. Committed to improving your daily living standards.", City = "Dhaka", District = "Dhaka", Phone = "+880 1734-567890", Bkash = "01734567890", Rating = 4.6m, Address = "78/B, Dhanmondi, Dhaka-1209" },
                new { Email = "mobilebazar@sparkle.local", Password = "Vendor@123", FullName = "Jamal Uddin", ShopName = "Mobile Bazar Bangladesh", ShopDescription = "Authorized reseller of Samsung, Xiaomi, Realme, Oppo. Best mobile prices in Bangladesh with EMI facility available.", City = "Sylhet", District = "Sylhet", Phone = "+880 1745-678901", Bkash = "01745678901", Rating = 4.8m, Address = "12, Zindabazar, Sylhet-3100" },
                new { Email = "freshmart@sparkle.local", Password = "Vendor@123", FullName = "Nasrin Akter", ShopName = "Fresh Mart BD", ShopDescription = "Daily fresh groceries delivered to your doorstep. Organic vegetables, premium spices, and halal meat products.", City = "Dhaka", District = "Dhaka", Phone = "+880 1756-789012", Bkash = "01756789012", Rating = 4.4m, Address = "56, Banani, Dhaka-1213" },
                new { Email = "beautyzone@sparkle.local", Password = "Vendor@123", FullName = "Sumaiya Khan", ShopName = "Beauty Zone Cosmetics", ShopDescription = "100% original cosmetics and skincare products. Authorized distributor of international brands in Bangladesh.", City = "Dhaka", District = "Dhaka", Phone = "+880 1767-890123", Bkash = "01767890123", Rating = 4.9m, Address = "34, Gulshan-2, Dhaka-1212" },
                new { Email = "sportsworld@sparkle.local", Password = "Vendor@123", FullName = "Rubel Islam", ShopName = "Sports World BD", ShopDescription = "Complete sports equipment and fitness gear. From cricket to gym equipment, we have everything for athletes.", City = "Khulna", District = "Khulna", Phone = "+880 1778-901234", Bkash = "01778901234", Rating = 4.3m, Address = "89, Sonadanga, Khulna-9100" },
                new { Email = "computerplus@sparkle.local", Password = "Vendor@123", FullName = "Tamim Hasan", ShopName = "Computer Plus Solutions", ShopDescription = "Laptops, desktops, accessories, and IT solutions. Trusted by 5000+ customers across Bangladesh.", City = "Dhaka", District = "Dhaka", Phone = "+880 1789-012345", Bkash = "01789012345", Rating = 4.7m, Address = "67, IDB Bhaban, Agargaon, Dhaka-1207" },
                new { Email = "bookshop@sparkle.local", Password = "Vendor@123", FullName = "Farhan Chowdhury", ShopName = "Book Lovers Paradise", ShopDescription = "Largest collection of Bengali and English books. Academic, novels, Islamic books, and stationery supplies.", City = "Rajshahi", District = "Rajshahi", Phone = "+880 1790-123456", Bkash = "01790123456", Rating = 4.6m, Address = "23, Shaheb Bazar, Rajshahi-6100" },
                new { Email = "babyshop@sparkle.local", Password = "Vendor@123", FullName = "Mehjabin Sultana", ShopName = "Baby Care Heaven", ShopDescription = "Everything for your little ones - diapers, toys, baby food, and mom care products. Trusted by Bangladeshi parents.", City = "Dhaka", District = "Dhaka", Phone = "+880 1701-234567", Bkash = "01701234567", Rating = 4.8m, Address = "92, Uttara, Sector-7, Dhaka-1230" },
                
                // Additional 15 sellers for expanded catalog
                new { Email = "jewelrypalace@sparkle.local", Password = "Vendor@123", FullName = "Sabrina Ahmed", ShopName = "Jewelry Palace BD", ShopDescription = "Exquisite gold, silver, and diamond jewelry. Traditional and modern designs. Trusted jeweler since 2010.", City = "Dhaka", District = "Dhaka", Phone = "+880 1812-345678", Bkash = "01812345678", Rating = 4.9m, Address = "156, New Market, Dhaka-1205" },
                new { Email = "pharmaeasy@sparkle.local", Password = "Vendor@123", FullName = "Dr. Ashraf Hossain", ShopName = "PharmaEasy Bangladesh", ShopDescription = "Licensed pharmacy with 24/7 medicine delivery. Genuine medicines, health supplements, and wellness products.", City = "Dhaka", District = "Dhaka", Phone = "+880 1823-456789", Bkash = "01823456789", Rating = 4.8m, Address = "45, Farmgate, Dhaka-1215" },
                new { Email = "petworld@sparkle.local", Password = "Vendor@123", FullName = "Tanvir Rahman", ShopName = "Pet World Bangladesh", ShopDescription = "Complete pet care solutions. Pet food, accessories, grooming products for dogs, cats, birds, and fish.", City = "Dhaka", District = "Dhaka", Phone = "+880 1834-567890", Bkash = "01834567890", Rating = 4.5m, Address = "78, Banasree, Dhaka-1219" },
                new { Email = "camerahub@sparkle.local", Password = "Vendor@123", FullName = "Imran Khan", ShopName = "Camera Hub BD", ShopDescription = "Professional cameras, lenses, drones, and photography equipment. Authorized Canon, Nikon, Sony dealer.", City = "Dhaka", District = "Dhaka", Phone = "+880 1845-678901", Bkash = "01845678901", Rating = 4.7m, Address = "234, Elephant Road, Dhaka-1205" },
                new { Email = "autoparts@sparkle.local", Password = "Vendor@123", FullName = "Rahim Uddin", ShopName = "Auto Parts Bangladesh", ShopDescription = "Genuine car and bike parts, accessories, engine oil, and automotive tools. Quick delivery across Bangladesh.", City = "Chittagong", District = "Chittagong", Phone = "+880 1856-789012", Bkash = "01856789012", Rating = 4.4m, Address = "67, Muradpur, Chittagong-4212" },
                new { Email = "musicstore@sparkle.local", Password = "Vendor@123", FullName = "Anika Tabassum", ShopName = "Music Store Bangladesh", ShopDescription = "Musical instruments, sound systems, DJ equipment, guitars, keyboards. Professional setup and training available.", City = "Dhaka", District = "Dhaka", Phone = "+880 1867-890123", Bkash = "01867890123", Rating = 4.6m, Address = "89, Dhanmondi 27, Dhaka-1209" },
                new { Email = "gardenstore@sparkle.local", Password = "Vendor@123", FullName = "Hasan Mahmud", ShopName = "Garden Store BD", ShopDescription = "Plants, seeds, gardening tools, fertilizers, and outdoor decor. Create your dream garden with expert guidance.", City = "Dhaka", District = "Dhaka", Phone = "+880 1878-901234", Bkash = "01878901234", Rating = 4.5m, Address = "123, Mirpur 10, Dhaka-1216" },
                new { Email = "travelgear@sparkle.local", Password = "Vendor@123", FullName = "Nadia Islam", ShopName = "Travel Gear Bangladesh", ShopDescription = "Quality luggage, travel bags, backpacks, and travel accessories. Durable and stylish for all your journeys.", City = "Sylhet", District = "Sylhet", Phone = "+880 1889-012345", Bkash = "01889012345", Rating = 4.7m, Address = "45, Zindabazar, Sylhet-3100" },
                new { Email = "artcraft@sparkle.local", Password = "Vendor@123", FullName = "Mahbub Alam", ShopName = "Art & Craft Corner", ShopDescription = "Art supplies, craft materials, painting tools, DIY kits. Everything for artists and hobbyists.", City = "Dhaka", District = "Dhaka", Phone = "+880 1890-123456", Bkash = "01890123456", Rating = 4.6m, Address = "56, Kataban, Dhaka-1217" },
                new { Email = "fitnessgear@sparkle.local", Password = "Vendor@123", FullName = "Sakib Ahmed", ShopName = "Fitness Gear Pro", ShopDescription = "Professional gym equipment, fitness supplements, yoga mats, dumbbells. Build your home gym with us.", City = "Dhaka", District = "Dhaka", Phone = "+880 1801-234567", Bkash = "01801234567", Rating = 4.8m, Address = "34, Bashundhara, Dhaka-1229" },
                new { Email = "toysrus@sparkle.local", Password = "Vendor@123", FullName = "Farhana Begum", ShopName = "Toys R Us Bangladesh", ShopDescription = "Educational toys, action figures, dolls, puzzles, and board games. Safe and certified toys for all ages.", City = "Chittagong", District = "Chittagong", Phone = "+880 1913-345678", Bkash = "01913345678", Rating = 4.7m, Address = "78, GEC Circle, Chittagong-4000" },
                new { Email = "electroniccity@sparkle.local", Password = "Vendor@123", FullName = "Jahangir Alam", ShopName = "Electronic City BD", ShopDescription = "TVs, refrigerators, washing machines, ACs, and kitchen appliances. Authorized dealer of LG, Samsung, Walton.", City = "Dhaka", District = "Dhaka", Phone = "+880 1924-456789", Bkash = "01924456789", Rating = 4.6m, Address = "123, Shantinagar, Dhaka-1217" },
                new { Email = "medicalequip@sparkle.local", Password = "Vendor@123", FullName = "Dr. Sultana Kamal", ShopName = "Medical Equipment BD", ShopDescription = "Medical devices, wheelchairs, blood pressure monitors, diabetes care, orthopedic supports, and health monitoring devices.", City = "Dhaka", District = "Dhaka", Phone = "+880 1935-567890", Bkash = "01935567890", Rating = 4.8m, Address = "67, Mohakhali, Dhaka-1212" },
                new { Email = "officemaster@sparkle.local", Password = "Vendor@123", FullName = "Kamrul Hassan", ShopName = "Office Master BD", ShopDescription = "Office furniture, chairs, desks, filing cabinets, and complete office setup solutions for businesses.", City = "Dhaka", District = "Dhaka", Phone = "+880 1946-678901", Bkash = "01946678901", Rating = 4.5m, Address = "89, Panthapath, Dhaka-1215" },
                new { Email = "kidszone@sparkle.local", Password = "Vendor@123", FullName = "Roksana Akter", ShopName = "Kids Zone Fashion", ShopDescription = "Trendy kids clothing, shoes, school uniforms, and accessories. Quality fashion for babies to teenagers.", City = "Dhaka", District = "Dhaka", Phone = "+880 1957-789012", Bkash = "01957789012", Rating = 4.7m, Address = "45, Elephant Road, Dhaka-1205" }
            };

            var sellers = new List<Seller>();
            foreach (var config in sellerConfigs)
            {
                var sellerUser = new ApplicationUser
                {
                    UserName = config.Email,
                    Email = config.Email,
                    EmailConfirmed = true,
                    FullName = config.FullName,
                    IsSeller = true
                };

                var sellerCreate = await userManager.CreateAsync(sellerUser, config.Password);
                if (sellerCreate.Succeeded)
                {
                    await userManager.AddToRoleAsync(sellerUser, "Seller");

                    var seller = new Seller
                    {
                        UserId = sellerUser.Id,
                        ShopName = config.ShopName,
                        ShopDescription = config.ShopDescription,
                        Status = SellerStatus.Approved,
                        StoreLogo = $"https://ui-avatars.com/api/?name={Uri.EscapeDataString(config.ShopName)}&size=200&background=random",
                        StoreBanner = $"https://placehold.co/1200x300/6366f1/ffffff?text={Uri.EscapeDataString(config.ShopName)}",
                        City = config.City,
                        District = config.District,
                        Country = "Bangladesh",
                        MobileNumber = config.Phone,
                        BkashMerchantNumber = config.Bkash,
                        BusinessAddress = config.Address,
                        Rating = config.Rating,
                        CreatedAt = DateTime.UtcNow.AddMonths(-new Random().Next(6, 24)),
                        ApprovedAt = DateTime.UtcNow.AddMonths(-new Random().Next(1, 6))
                    };

                    db.Sellers.Add(seller);
                    sellers.Add(seller);
                }
            }

            await db.SaveChangesAsync();
        }

        // Product seeding using ProductSeedingService
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var productSeeder = new Sparkle.Api.Services.ProductSeedingService(db, loggerFactory.CreateLogger<Sparkle.Api.Services.ProductSeedingService>());
        await productSeeder.SeedProductsAsync();


        // Seed a demo customer user for testing
        var customerEmail = "user@sparkle.local";
        var customerUser = await userManager.FindByEmailAsync(customerEmail);
        if (customerUser is null)
        {
            customerUser = new ApplicationUser
            {
                UserName = customerEmail,
                Email = customerEmail,
                EmailConfirmed = true,
                FullName = "Demo Customer"
            };
            var customerCreate = await userManager.CreateAsync(customerUser, "User@123");
            if (customerCreate.Succeeded)
            {
                await userManager.AddToRoleAsync(customerUser, "User");
            }
        }

        // Seed Soyan's user account
        var soyanEmail = "misoyan07@gmail.com";
        var soyanUser = await userManager.FindByEmailAsync(soyanEmail);
        if (soyanUser is null)
        {
            soyanUser = new ApplicationUser
            {
                UserName = soyanEmail,
                Email = soyanEmail,
                EmailConfirmed = true,
                FullName = "Soyan"
            };
            var soyanCreate = await userManager.CreateAsync(soyanUser, "89694929Soyan@07");
            if (soyanCreate.Succeeded)
            {
                await userManager.AddToRoleAsync(soyanUser, "User");
            }
        }

        if (!await db.DeliveryZones.AnyAsync())
        {
            var locationSeeder = services.GetRequiredService<Sparkle.Infrastructure.Services.LocationSeedingService>();
            var zones = locationSeeder.GetZones();
            db.DeliveryZones.AddRange(zones);
            await db.SaveChangesAsync();

            var dhakaZone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Name == "Inside Dhaka");
            var suburbsZone = await db.DeliveryZones.FirstOrDefaultAsync(z => z.Name == "Dhaka Suburbs");

            if (dhakaZone != null)
            {
                var areas = locationSeeder.GetDhakaAreas();
                foreach (var area in areas) area.ZoneId = dhakaZone.Id;
                db.DeliveryAreas.AddRange(areas);
            }

            if (suburbsZone != null)
            {
                var areas = locationSeeder.GetDhakaSuburbs();
                foreach (var area in areas) area.ZoneId = suburbsZone.Id;
                db.DeliveryAreas.AddRange(areas);
            }

            await db.SaveChangesAsync();
            logger.LogInformation("Seeded delivery zones and areas");
        }

        // Seed Default Site Settings
        if (!await db.SiteSettings.AnyAsync()) 
        {
             var settingsService = services.GetRequiredService<ISettingsService>();
             await settingsService.SetStringValueAsync("SiteTitle", "Sparkle", "General", "string", "The name of the website");
             await settingsService.SetStringValueAsync("SupportPhone", "16789", "General", "string", "Customer support phone number");
             await settingsService.SetStringValueAsync("SupportEmail", "support@sparkle.com", "General", "string", "Customer support email address");
             await settingsService.SetStringValueAsync("FooterCopyright", "Sparkle.com", "General", "string", "Copyright text for footer");
        }

        // Seed Dynamic Forms
        var dynamicFormSeeder = services.GetRequiredService<Sparkle.Api.Services.DynamicFormSeedingService>();
        await dynamicFormSeeder.SeedFormsAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database seeding.");
    }
}

// Swagger/OpenAPI Documentation (Consolidated)
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Sparkle API V1");
    options.RoutePrefix = "swagger"; // Standard access at /swagger
});

if (app.Environment.IsDevelopment())
{
    // Development specific settings if any
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    // Only redirect to HTTPS in production
    app.UseHttpsRedirection();
}

// Enable response compression (must be before UseStaticFiles)
// Disable in Development to allow Hot Reload script injection
if (!app.Environment.IsDevelopment())
{
    app.UseResponseCompression();
}

// Static files with caching
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Cache static files for 7 days
        const int durationInSeconds = 60 * 60 * 24 * 7;
        ctx.Context.Response.Headers.Append("Cache-Control", $"public,max-age={durationInSeconds}");
    }
});

app.UseRouting();

app.UseResponseCaching(); // Re-enabled to fix VaryByQueryKeys error

app.UseSession(); // Enable session for checkout flow

// Global exception handler - must be early in pipeline
app.UseMiddleware<Sparkle.Api.Middleware.GlobalExceptionMiddleware>();

// Security headers middleware
app.UseMiddleware<Sparkle.Api.Middleware.SecurityMiddleware>();

// Admin action logging middleware
app.UseMiddleware<Sparkle.Api.Middleware.AdminActionLoggingMiddleware>();


app.UseAuthentication();
app.UseAuthorization();

// MVC routes for UI panels
app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Attribute-routed API controllers
app.MapControllers();

// SignalR Hub Routes
app.MapHub<Sparkle.Api.Hubs.InventoryHub>("/hubs/inventory");
app.MapHub<Sparkle.Api.Hubs.OrderTrackingHub>("/hubs/ordertracking");
app.MapHub<Sparkle.Api.Hubs.NotificationHub>("/hubs/notification");
app.MapHub<Sparkle.Api.Hubs.ChatHub>("/hubs/chat");

app.Run();
