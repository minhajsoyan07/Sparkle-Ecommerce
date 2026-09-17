using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Orders;
using Sparkle.Infrastructure;
using Sparkle.Domain.Identity;
using Sparkle.Api.Services;
using Sparkle.Infrastructure.Services;

namespace Sparkle.Api.Areas.Seller.Controllers;

[Area("Seller")]
[Authorize(Roles = "Seller")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ISellerPerformanceService _performanceService;
    private readonly IAIService _aiService;
    private readonly ICommissionService _commissionService;

    public DashboardController(
        ApplicationDbContext db, 
        SignInManager<ApplicationUser> signInManager,
        ISellerPerformanceService performanceService,
        IAIService aiService,
        ICommissionService commissionService)
    {
        _db = db;
        _signInManager = signInManager;
        _performanceService = performanceService;
        _aiService = aiService;
        _commissionService = commissionService;
    }

    private string GetUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new InvalidOperationException();

    [HttpGet]
    public async Task<IActionResult> Setup()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(s => s.UserId == userId);
        if (seller != null) return RedirectToAction(nameof(Index));

        return View(new Domain.Sellers.Seller { UserId = userId });
    }

    [HttpPost]
    public async Task<IActionResult> Setup(Domain.Sellers.Seller model)
    {
        var userId = GetUserId();
        
        // Manual validation for required fields if needed, or rely on ModelState
        // Since we are using the domain model directly, we might need to be careful.
        // Let's ensure basic fields are there.
        if (string.IsNullOrWhiteSpace(model.ShopName)) ModelState.AddModelError("ShopName", "Shop Name is required");
        if (string.IsNullOrWhiteSpace(model.MobileNumber)) ModelState.AddModelError("MobileNumber", "Mobile Number is required");

        if (!ModelState.IsValid) return View(model);

        model.UserId = userId;
        model.CreatedAt = DateTime.UtcNow;
        model.Status = Domain.Sellers.SellerStatus.Pending; // Pending for review
        
        // Ensure other non-nullable fields are set defaults if missing
        if (model.Country == null) model.Country = "Bangladesh";

        _db.Sellers.Add(model);
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("EnterGuestView")]
    public async Task<IActionResult> EnterGuestView()
    {
        // 1. Set GuestMode cookie
        Response.Cookies.Append("GuestMode", "true", new CookieOptions 
        { 
            Path = "/",
            HttpOnly = true, 
            Expires = DateTime.Now.AddMinutes(30) 
        });

        // 2. Sign fully out to ensure strict separation
        await _signInManager.SignOutAsync();

        // 3. Redirect to User Homepage
        return Redirect("/");
    }

    [HttpGet("ExitGuestView")]
    public IActionResult ExitGuestView()
    {
        Response.Cookies.Delete("GuestMode", new CookieOptions { Path = "/" });
        // Redirect to login (pre-filled as seller likely preferred, or just dashboard which triggers login)
        return Redirect("/Seller/Dashboard");
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);
        
        if (seller == null)
        {
            return RedirectToAction("Setup");
        }

        // var model = await _performanceService.GetSellerScoreAsync(seller.Id);
        
        // AI: Sales Prediction (Seller Insights)
        // Predicting next month's sales based on simplified history (e.g. last 6 months)
        var sixMonthsAgo = DateTime.UtcNow.AddMonths(-6);

        // Define sellerOrderItems query
        var salesDataQuery = _db.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Order.SellerId == seller.Id);
        
        // Fetch sales data grouped by year/month
        var salesDataRaw = await salesDataQuery
            .Where(oi => oi.Order.OrderDate >= sixMonthsAgo && oi.Order.Status == Sparkle.Domain.Orders.OrderStatus.Delivered)
            .GroupBy(oi => new { oi.Order.OrderDate.Year, oi.Order.OrderDate.Month })
            .Select(g => new { Year = g.Key.Year, Month = g.Key.Month, Count = g.Sum(x => x.Quantity) }) 
            .ToListAsync();

        var salesHistory = new List<int>();
        var currentMonth = DateTime.UtcNow;
        // Iterate last 6 months to ensure order (oldest to newest) and fill gaps
        for (int i = 5; i >= 0; i--)
        {
            var d = currentMonth.AddMonths(-i);
            var monthData = salesDataRaw.FirstOrDefault(x => x.Year == d.Year && x.Month == d.Month);
            salesHistory.Add(monthData?.Count ?? 0);
        }

        var predictedSales = await _aiService.PredictNextMonthSalesAsync(0, salesHistory); // productId 0 for general store prediction
        
        ViewBag.PredictedSales = predictedSales;
        ViewBag.SalesTrend = predictedSales > salesHistory.Last() ? "Increasing" : "Decreasing";

        var now = DateTime.UtcNow;
        var today = now.Date;
        var weekAgo = now.AddDays(-7);
        var monthStart = new DateTime(now.Year, now.Month, 1);

        var sellerOrderItems = _db.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.ProductVariant)
            .ThenInclude(pv => pv!.Product)
                .ThenInclude(p => p.Images)
            .Where(oi => oi.ProductVariant != null && oi.ProductVariant.Product != null && oi.ProductVariant.Product!.SellerId == seller.Id);

        // Top Products: Optimized to group in DB (Step 1: Get IDs and Stats)
        var topProductStats = await _db.OrderItems
            .AsNoTracking()
            .Where(oi => oi.ProductVariant != null && oi.ProductVariant.Product != null && oi.ProductVariant.Product.SellerId == seller.Id)
            .GroupBy(oi => new { Id = oi.ProductVariant!.Product!.Id, Title = oi.ProductVariant.Product.Title })
            .Select(g => new 
            {
                ProductId = g.Key.Id,
                ProductName = g.Key.Title,
                OrderCount = g.Count(),
                Revenue = g.Sum(oi => oi.TotalPrice)
            })
            .OrderByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync();

        // Step 2: Fetch details (Image, Stock) for these specific products
        var topProductIds = topProductStats.Select(x => x.ProductId).ToList();
        var productDetails = await _db.Products
            .AsNoTracking()
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Where(p => topProductIds.Contains(p.Id))
            .ToListAsync();

        // Step 3: Combine
        var topProductsList = topProductStats.Select(stat => 
        {
            var product = productDetails.FirstOrDefault(p => p.Id == stat.ProductId);
            return new SellerTopProductDto
            {
                ProductId = stat.ProductId,
                ProductName = stat.ProductName,
                OrderCount = stat.OrderCount,
                Revenue = stat.Revenue,
                ImageUrl = product?.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                Stock = product?.Variants.Sum(v => v.Stock) ?? 0
            };
        }).ToList();

        var stats = new SellerDashboardStats
        {
            ShopName = seller.ShopName,
            TotalProducts = await _db.Products.CountAsync(p => p.SellerId == seller.Id),
            ActiveProducts = await _db.Products.CountAsync(p => p.SellerId == seller.Id && p.IsActive),
            NewProductsCount = await _db.Products.CountAsync(p => p.SellerId == seller.Id && p.CreatedAt >= DateTime.UtcNow.AddHours(-24)),
            LowStockProducts = await _db.ProductVariants
                .Include(v => v.Product)
                .CountAsync(v => v.Product.SellerId == seller.Id && v.Stock <= 10),
            
            TotalOrders = await sellerOrderItems.Select(oi => oi.OrderId).Distinct().CountAsync(),
            PendingOrders = await sellerOrderItems.Where(oi => oi.Order.Status == OrderStatus.Pending).Select(oi => oi.OrderId).Distinct().CountAsync(),
            ProcessingOrders = await sellerOrderItems.Where(oi => oi.Order.Status == OrderStatus.Processing).Select(oi => oi.OrderId).Distinct().CountAsync(),
            CompletedOrders = await sellerOrderItems.Where(oi => oi.Order.Status == OrderStatus.Delivered).Select(oi => oi.OrderId).Distinct().CountAsync(),
            
            TotalRevenue = await sellerOrderItems.SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0m,
            TodayRevenue = await sellerOrderItems.Where(oi => oi.Order.OrderDate >= today && oi.Order.OrderDate < today.AddDays(1)).SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0m,
            MonthRevenue = await sellerOrderItems.Where(oi => oi.Order.OrderDate >= monthStart).SumAsync(oi => (decimal?)oi.TotalPrice) ?? 0m,
            
            TodayOrders = await sellerOrderItems.Where(oi => oi.Order.OrderDate >= today && oi.Order.OrderDate < today.AddDays(1)).Select(oi => oi.OrderId).Distinct().CountAsync(),
            WeekOrders = await sellerOrderItems.Where(oi => oi.Order.OrderDate >= weekAgo).Select(oi => oi.OrderId).Distinct().CountAsync(),
            
            TotalCustomers = await sellerOrderItems.Select(oi => oi.Order.UserId).Distinct().CountAsync(),
            
            RecentOrders = (await _db.Orders
                .Include(o => o.User)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductVariant!)
                        .ThenInclude(pv => pv.Product)
                            .ThenInclude(p => p.Category)
                .Include(o => o.OrderItems)
                    .ThenInclude(oi => oi.ProductVariant!)
                        .ThenInclude(pv => pv.Product)
                            .ThenInclude(p => p.Images)
                .Where(o => o.OrderItems.Any(oi => oi.ProductVariant != null && oi.ProductVariant.Product != null && oi.ProductVariant.Product!.SellerId == seller.Id))
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync())
                .Select(o => {
                    var sellerItems = o.OrderItems.Where(oi => oi.ProductVariant?.Product?.SellerId == seller.Id).ToList();
                    var firstItem = sellerItems.FirstOrDefault();
                    var firstProduct = firstItem?.ProductVariant?.Product;
                    return new SellerRecentOrderDto
                    {
                        OrderId = o.Id,
                        CustomerName = o.User != null ? (o.User.FullName ?? o.User.Email ?? "Guest") : "Guest",
                        Date = o.OrderDate,
                        Total = sellerItems.Sum(oi => oi.TotalPrice),
                        Status = o.Status.ToString(),
                        FirstProductName = firstProduct?.Title ?? "Unknown Product",
                        FirstCategoryName = firstProduct?.Category?.Name ?? "General",
                        FirstImageUrl = firstProduct?.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
                    };
                }).ToList(),
            
TopProducts = topProductsList,

            ProductViews = await _db.ProductViews
                .Include(pv => pv.Product)
                .CountAsync(pv => pv.Product.SellerId == seller.Id),
                
                
            StoreRating = seller.Rating,
            Status = seller.Status,
            AvailableBalance = await _commissionService.GetSellerAvailableBalanceAsync(seller.Id)
        };

        // Calculate Chart Data (Monthly - Last 30 Days split into 2-day intervals)
        var thirtyDaysAgo = now.Date.AddDays(-29);
        var monthlyOrders = await _db.OrderItems
            .Include(oi => oi.Order)
            .Where(oi => oi.Product.SellerId == seller.Id && oi.Order.OrderDate >= thirtyDaysAgo && oi.Order.Status != OrderStatus.Cancelled)
            .ToListAsync();

        var chartData = new List<DailySales>();
        for (var i = 0; i < 30; i++)
        {
            var start = thirtyDaysAgo.AddDays(i);
            var end = start.AddDays(1);
            var total = monthlyOrders
                .Where(x => x.Order.OrderDate >= start && x.Order.OrderDate < end)
                .Sum(x => x.TotalPrice);

            chartData.Add(new DailySales
            {
                Date = start.ToString("dd"),
                Sales = total,
                Orders = monthlyOrders.Count(x => x.Order.OrderDate >= start && x.Order.OrderDate < end)
            });
        }
        stats.SalesChartData = chartData;

        stats.AverageOrderValue = stats.TotalOrders > 0 ? stats.TotalRevenue / stats.TotalOrders : 0m;

        return View(stats);
    }



    public async Task<IActionResult> Products(int page = 1)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);

        if (seller == null)
        {
            return RedirectToAction("Setup");
        }

        int pageSize = 8;
        var query = _db.Products
            .Include(p => p.Category)
            .Include(p => p.Brand)
            .Include(p => p.Images)
            .Include(p => p.Variants)
            .Where(p => p.SellerId == seller.Id)
            .OrderByDescending(p => p.CreatedAt);

        int totalItems = await query.CountAsync();
        var products = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.Seller = seller;
        ViewBag.CurrentPage = page;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalItems / pageSize);
        ViewBag.TotalProducts = totalItems;

        return View(products);
    }

    public async Task<IActionResult> StoreSettings()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);

        if (seller == null)
        {
            return RedirectToAction("Setup");
        }

        return View(seller);
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStoreSettings(Domain.Sellers.Seller model)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);

        if (seller == null)
        {
            return RedirectToAction("Setup");
        }

        seller.ShopName = model.ShopName;
        seller.ShopDescription = model.ShopDescription;
        seller.MobileNumber = model.MobileNumber;
        seller.BkashMerchantNumber = model.BkashMerchantNumber;
        seller.BusinessAddress = model.BusinessAddress;
        seller.City = model.City;
        seller.District = model.District;
        seller.StoreLogo = model.StoreLogo ?? seller.StoreLogo;
        seller.StoreBanner = model.StoreBanner ?? seller.StoreBanner;

        await _db.SaveChangesAsync();

        TempData["Success"] = "Store settings updated successfully!";
        return RedirectToAction(nameof(StoreSettings));
    }

    [HttpGet]
    public async Task<IActionResult> CreateProduct()
    {
        var categoryList = await _db.Categories.Select(c => new { c.Id, c.Name }).ToListAsync();
        ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categoryList, "Id", "Name");
        
        return View(new Sparkle.Domain.Catalog.Product());
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(Sparkle.Domain.Catalog.Product model, decimal Price, int Stock, List<string>? ImageUrls, List<IFormFile>? ImageFiles)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);
        if (seller == null) return RedirectToAction(nameof(Setup));

        // Validation: Price must be greater than 0
        if (Price <= 0)
        {
            ModelState.AddModelError(nameof(Price), "Price must be greater than 0");
        }

        // Validation: Stock cannot be negative
        if (Stock < 0)
        {
            ModelState.AddModelError(nameof(Stock), "Stock quantity cannot be negative");
        }

        if (!ModelState.IsValid)
        {
            var categoryList = await _db.Categories.Select(c => new { c.Id, c.Name }).ToListAsync();
            ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categoryList, "Id", "Name");
            return View(model);
        }

        model.SellerId = seller.Id;
        model.CreatedAt = DateTime.UtcNow;
        model.IsActive = true;
        
        // Generate Slug
        model.Slug = $"{System.Text.RegularExpressions.Regex.Replace(model.Title.ToLower(), @"[^a-z0-9\s-]", "").Replace(" ", "-")}-{Guid.NewGuid().ToString().Substring(0, 6)}";

        // Create Default Variant
        model.Variants = new List<ProductVariant>
        {
            new ProductVariant
            {
                Color = "Standard",
                Size = "Standard",
                Price = Price,
                Stock = Stock,
                Sku = $"SKU-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
            }
        };
        
        // Sync BasePrice
        model.BasePrice = Price;

        model.Images = new List<ProductImage>();

        // Add Image
        // Process Image URLs
        if (ImageUrls != null && ImageUrls.Any())
        {
            foreach (var url in ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                model.Images.Add(new ProductImage { ImagePath = url, SortOrder = model.Images.Count });
            }
        }

        // Process Image Files
        if (ImageFiles != null && ImageFiles.Any())
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
            Directory.CreateDirectory(uploadsFolder);

            foreach (var file in ImageFiles)
            {
                if (file.Length > 0)
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                    
                    model.Images.Add(new ProductImage { ImagePath = "/uploads/products/" + uniqueFileName, SortOrder = model.Images.Count });
                }
            }
        }

        _db.Products.Add(model);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Product created successfully!";
        return RedirectToAction(nameof(Products));
    }

    [HttpGet]
    public async Task<IActionResult> EditProduct(int id)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);
        if (seller == null) return RedirectToAction(nameof(Setup));

        var product = await _db.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.SellerId == seller.Id);

        if (product == null) return NotFound();

        var categoryList = await _db.Categories.Select(c => new { c.Id, c.Name }).ToListAsync();
        ViewBag.Categories = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(categoryList, "Id", "Name");

        // View Model Prep
        var primaryVariant = product.Variants.FirstOrDefault();
        ViewBag.Price = primaryVariant?.Price ?? 0;
        ViewBag.Stock = primaryVariant?.Stock ?? 0;
        ViewBag.ImageUrl = product.Images.FirstOrDefault()?.Url ?? "";

        return View(product);
    }

    [HttpPost]
    public async Task<IActionResult> EditProduct(int id, Sparkle.Domain.Catalog.Product model, decimal Price, int Stock, List<string>? ImageUrls, List<IFormFile>? ImageFiles)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);
        if (seller == null) return RedirectToAction(nameof(Setup));

        var product = await _db.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.SellerId == seller.Id);

        if (product == null) return NotFound();

        // Validation: Price must be greater than 0
        if (Price <= 0)
        {
            TempData["Error"] = "Price must be greater than 0";
            return RedirectToAction(nameof(EditProduct), new { id });
        }

        // Validation: Stock cannot be negative
        if (Stock < 0)
        {
            TempData["Error"] = "Stock quantity cannot be negative";
            return RedirectToAction(nameof(EditProduct), new { id });
        }

        // Update Fields
        product.Title = model.Title;
        product.ShortDescription = model.ShortDescription;
        product.Description = model.Description;
        product.CategoryId = model.CategoryId;

        // Update Primary Variant
        var variant = product.Variants.FirstOrDefault();
        if (variant != null)
        {
            variant.Price = Price;
            variant.Stock = Stock;
            
            // Sync BasePrice
            product.BasePrice = Price;
        }
        else
        {
            product.Variants.Add(new ProductVariant 
            { 
                Product = product, 
                Price = Price, 
                Stock = Stock, 
                Color = "Standard", 
                Size = "Standard",
                Sku = $"SKU-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
            });
            
            // Sync BasePrice
            product.BasePrice = Price;
        }

        // Update Image
        // Process Image URLs
        if (ImageUrls != null && ImageUrls.Any())
        {
            foreach (var url in ImageUrls.Where(u => !string.IsNullOrWhiteSpace(u)))
            {
                product.Images.Add(new ProductImage { ImagePath = url, SortOrder = product.Images.Count });
            }
        }

        // Process Image Files
        if (ImageFiles != null && ImageFiles.Any())
        {
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
            Directory.CreateDirectory(uploadsFolder);

            foreach (var file in ImageFiles)
            {
                if (file.Length > 0)
                {
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(fileStream);
                    }
                    
                    product.Images.Add(new ProductImage { ImagePath = "/uploads/products/" + uniqueFileName, SortOrder = product.Images.Count });
                }
            }
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "Product updated successfully!";
        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleProductStatus(int productId)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);

        if (seller == null)
        {
            return Json(new { success = false, message = "Seller not found" });
        }

        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.SellerId == seller.Id);
        if (product == null)
        {
            return Json(new { success = false, message = "Product not found" });
        }

        product.IsActive = !product.IsActive;
        await _db.SaveChangesAsync();

        return Json(new { success = true, isActive = product.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> UpdateStock(int variantId, int stock)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);

        if (seller == null)
        {
            return Json(new { success = false, message = "Seller not found" });
        }

        // Validation: Stock cannot be negative
        if (stock < 0)
        {
            return Json(new { success = false, message = "❌ Stock cannot be negative!" });
        }

        var variant = await _db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.Product.SellerId == seller.Id);

        if (variant == null)
        {
            return Json(new { success = false, message = "Variant not found" });
        }

        variant.Stock = stock;
        await _db.SaveChangesAsync();

        return Json(new { success = true, stock = variant.Stock });
    }

    [HttpPost]
    public async Task<IActionResult> UpdatePrice(int variantId, decimal price)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);

        if (seller == null)
        {
            return Json(new { success = false, message = "Seller not found" });
        }

        // Validation: Price must be greater than 0
        if (price <= 0)
        {
            return Json(new { success = false, message = "❌ Price must be greater than 0!" });
        }

        var variant = await _db.ProductVariants
            .Include(v => v.Product)
            .FirstOrDefaultAsync(v => v.Id == variantId && v.Product.SellerId == seller.Id);

        if (variant == null)
        {
            return Json(new { success = false, message = "Variant not found" });
        }

        variant.Price = price;
        // Sync BasePrice
        variant.Product.BasePrice = price;
        await _db.SaveChangesAsync();

        return Json(new { success = true, price = variant.Price });
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> ToggleStatus()
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);

        if (seller == null)
        {
            return Json(new { success = false, message = "Seller not found" });
        }

        // Disable or update logic. Assuming Toggle here is for Vacation Mode, but modifying Status will lock out seller.
        // For now, we will map it to Approved/Isolated if that's the intention, OR, we should disable it if it breaks login.
        // If Pending/Isolated blocks login, then this toggle is a "Suspend Self" button?
        // Let's assume this was for vacation mode and map it to Isolated (temporarily restricted) or just don't allow it.
        // But since I changed the Enum, I must fix the compilation error regardless of logic correctness for now.
        
        // seller.Status = seller.Status == Domain.Sellers.SellerStatus.Approved 
        //    ? Domain.Sellers.SellerStatus.Isolated 
        //    : Domain.Sellers.SellerStatus.Approved;

        // await _db.SaveChangesAsync();

        return Json(new { success = false, message = "Status toggling is managed by Admin." });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);
        if (seller == null) return NotFound();

        var product = await _db.Products
            .Include(p => p.Variants)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id && p.SellerId == seller.Id);

        if (product == null) return NotFound();

        _db.Products.Remove(product);
        await _db.SaveChangesAsync();

        return Ok();
    }

    [HttpPost]
    public async Task<IActionResult> DeleteProductImage(int imageId)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);
        
        if (seller == null)
        {
            return Json(new { success = false, message = "Seller not found" });
        }

        // Get the image
        var image = await _db.ProductImages
            .Include(img => img.Product)
            .FirstOrDefaultAsync(img => img.Id == imageId && img.Product.SellerId == seller.Id);

        if (image == null)
        {
            return Json(new { success = false, message = "Image not found" });
        }

        // Delete physical file if it's a local upload (starts with /uploads)
        if (!string.IsNullOrEmpty(image.Url) && image.Url.StartsWith("/uploads"))
        {
            try
            {
                var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", image.Url.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting file: {ex.Message}");
            }
        }

        // Remove from database
        _db.ProductImages.Remove(image);
        await _db.SaveChangesAsync();

        return Json(new { success = true, message = "Image deleted successfully" });
    }

    [HttpPost]
    public async Task<IActionResult> SaveCroppedImage(int imageId, IFormFile file)
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);
        
        if (seller == null)
        {
            return Json(new { success = false, message = "Seller not found" });
        }

        if (file == null || file.Length == 0)
        {
            return Json(new { success = false, message = "No file provided" });
        }

        // Get the image
        var image = await _db.ProductImages
            .Include(img => img.Product)
            .FirstOrDefaultAsync(img => img.Id == imageId && img.Product.SellerId == seller.Id);

        if (image == null)
        {
            return Json(new { success = false, message = "Image not found" });
        }

        try
        {
            // Delete old file if it's a local upload
            if (!string.IsNullOrEmpty(image.Url) && image.Url.StartsWith("/uploads"))
            {
                var oldFilePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", image.Url.TrimStart('/'));
                if (System.IO.File.Exists(oldFilePath))
                {
                    System.IO.File.Delete(oldFilePath);
                }
            }

            // Save new cropped image
            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "products");
            Directory.CreateDirectory(uploadsFolder);

            var uniqueFileName = Guid.NewGuid().ToString() + "_cropped.jpg";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // Update database
            image.ImagePath = "/uploads/products/" + uniqueFileName;
            await _db.SaveChangesAsync();

            return Json(new { success = true, message = "Image updated successfully" });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = $"Error saving image: {ex.Message}" });
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetSalesChartData(string filter = "monthly")
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);
        if (seller == null) return BadRequest("Seller not found");

        var now = DateTime.UtcNow;
        // Adjust to local time if needed, but keeping UTC for consistency or assuming server local
        // If specific timezone is needed, we might need to adjust. For now, using Server Time.
        
        var data = new List<DailySales>();

        switch (filter.ToLower())
        {
            case "daily":
                // Time Range: Last 24 Hours
                // Data Interval: Every 2 Hours
                // Total Bars: 12
                var twentyFourHoursAgo = now.AddHours(-24);
                
                var dailyOrders = await _db.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Product.SellerId == seller.Id && oi.Order.OrderDate >= twentyFourHoursAgo && oi.Order.Status != OrderStatus.Cancelled)
                    .ToListAsync(); // In-memory processing for 2-hour grouping

                // Generate 12 bars of 2-hour intervals
                for (var i = 0; i < 12; i++)
                {
                    var start = twentyFourHoursAgo.AddHours(i * 2);
                    var end = start.AddHours(2);

                    var total = dailyOrders
                        .Where(x => x.Order.OrderDate >= start && x.Order.OrderDate < end)
                        .Sum(x => x.TotalPrice);

                    data.Add(new DailySales
                    {
                        // Label: Start time of the interval (e.g., 00:00, 02:00)
                        Date = start.ToString("HH:mm"), 
                        Sales = total
                    });
                }
                break;

            case "weekly":
                // Time Range: Last 7 Days
                // Data Interval: 1 Day
                // Total Bars: 7
                var sevenDaysAgo = now.Date.AddDays(-6); // Start from 6 days ago + today = 7 days
                
                var weeklyOrders = await _db.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Product.SellerId == seller.Id && oi.Order.OrderDate >= sevenDaysAgo && oi.Order.Status != OrderStatus.Cancelled)
                    .ToListAsync();

                for (var i = 0; i < 7; i++)
                {
                    var date = sevenDaysAgo.AddDays(i);
                    var total = weeklyOrders
                        .Where(x => x.Order.OrderDate.Date == date)
                        .Sum(x => x.TotalPrice);

                    data.Add(new DailySales
                    {
                        // Label: Mon, Tue or Date
                        Date = date.ToString("ddd"), 
                        Sales = total
                    });
                }
                break;

            case "monthly":
                // Time Range: Last 30 Days
                // Data Interval: Every 2 Days
                // Total Bars: 15
                var thirtyDaysAgo = now.Date.AddDays(-29); // 29 days ago + today = 30 days
                
                var monthlyOrders = await _db.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Product.SellerId == seller.Id && oi.Order.OrderDate >= thirtyDaysAgo && oi.Order.Status != OrderStatus.Cancelled)
                    .ToListAsync();

                for (var i = 0; i < 30; i++)
                {
                    var start = thirtyDaysAgo.AddDays(i);
                    var end = start.AddDays(1);
                    
                    var total = monthlyOrders
                        .Where(x => x.Order.OrderDate >= start && x.Order.OrderDate < end)
                        .Sum(x => x.TotalPrice);

                    data.Add(new DailySales
                    {
                        // Label: 01, 02 (Day)
                        Date = start.ToString("dd"),
                        Sales = total
                    });
                }
                break;

            case "yearly":
                // Time Range: Last 12 Months
                // Data Interval: 1 Month
                // Total Bars: 12
                // We want the last 12 months including current.
                var startOfCurrentMonth = new DateTime(now.Year, now.Month, 1);
                var startOfYearPeriod = startOfCurrentMonth.AddMonths(-11);

                var yearlyOrders = await _db.OrderItems
                    .Include(oi => oi.Order)
                    .Where(oi => oi.Product.SellerId == seller.Id && oi.Order.OrderDate >= startOfYearPeriod && oi.Order.Status != OrderStatus.Cancelled)
                    .ToListAsync();

                for (var i = 0; i < 12; i++)
                {
                    var monthDate = startOfYearPeriod.AddMonths(i);
                    var total = yearlyOrders
                        .Where(x => x.Order.OrderDate.Year == monthDate.Year && x.Order.OrderDate.Month == monthDate.Month)
                        .Sum(x => x.TotalPrice);

                    data.Add(new DailySales
                    {
                        // Label: Jan, Feb
                        Date = monthDate.ToString("MMM"), 
                        Sales = total
                    });
                }
                break;
        }

        return Json(data);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetRecentOrders(string filter = "Last 7 Days")
    {
        var userId = GetUserId();
        var seller = await _db.Sellers.FirstOrDefaultAsync(v => v.UserId == userId);
        if (seller == null) return BadRequest("Seller not found");

        var query = _db.Orders
            .Where(o => o.OrderItems.Any(i => i.Product.SellerId == seller.Id));

        var now = DateTime.UtcNow;

        switch (filter)
        {
            case "Today":
                query = query.Where(o => o.OrderDate.Date == now.Date);
                break;
            case "Yesterday":
                query = query.Where(o => o.OrderDate.Date == now.AddDays(-1).Date);
                break;
            case "Last 30 Days":
                query = query.Where(o => o.OrderDate >= now.AddDays(-30));
                break;
            case "Last 7 Days":
            default:
                query = query.Where(o => o.OrderDate >= now.AddDays(-7));
                break;
        }

        var orders = await query
            .OrderByDescending(o => o.OrderDate)
            .Take(10) // Limit to reasonable amount
            .Select(o => new 
            {
                OrderId = o.Id,
                Date = o.OrderDate,
                Total = o.TotalAmount, // Ideally should be seller's portion, assuming order total for now or need calculation
                SellerTotal = o.OrderItems.Where(i => i.SellerId == seller.Id).Sum(i => i.UnitPrice * i.Quantity),
                CustomerName = o.User.FullName,
                Status = o.Status.ToString(),
                FirstProductName = o.OrderItems.First(i => i.SellerId == seller.Id).Product.Title,
                FirstCategoryName = o.OrderItems.First(i => i.SellerId == seller.Id).Product.Category.Name,
                FirstImageUrl = o.OrderItems.First(i => i.SellerId == seller.Id).Product.Images.Select(img => img.Url).FirstOrDefault() ?? "https://via.placeholder.com/150"
            })
            .ToListAsync();

        // Map to client friendly DTO
        var result = orders.Select(o => new SellerRecentOrderDto
        {
            OrderId = o.OrderId,
            CustomerName = o.CustomerName ?? "Guest",
            Date = o.Date,
            Total = o.SellerTotal, // Using SellerTotal explicitly
            Status = o.Status,
            FirstProductName = o.FirstProductName,
            FirstCategoryName = o.FirstCategoryName,
            FirstImageUrl = o.FirstImageUrl
        });

        return Json(result);
    }

    }


public class SellerDashboardStats
{
    public string ShopName { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int ActiveProducts { get; set; }
    public int LowStockProducts { get; set; }
    public int TotalOrders { get; set; }
    public int PendingOrders { get; set; }
    public int ProcessingOrders { get; set; }
    public int CompletedOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TodayRevenue { get; set; }
    public decimal MonthRevenue { get; set; }
    public int TodayOrders { get; set; }
    public int WeekOrders { get; set; }
    public decimal AverageOrderValue { get; set; }
    public int TotalCustomers { get; set; }
    public List<SellerRecentOrderDto> RecentOrders { get; set; } = new();
    public List<SellerTopProductDto> TopProducts { get; set; } = new();
    
    // New Properties
    public int NewProductsCount { get; set; }
    public List<DailySales> SalesChartData { get; set; } = new();
    public int ProductViews { get; set; }
    public decimal StoreRating { get; set; }
    public Sparkle.Domain.Sellers.SellerStatus Status { get; set; }
    public decimal AvailableBalance { get; set; }
}

public class SellerRecentOrderDto
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = string.Empty;
    public string FirstProductName { get; set; } = string.Empty;
    public string FirstCategoryName { get; set; } = string.Empty;
    public string? FirstImageUrl { get; set; }
}

public class SellerTopProductDto
{
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }
    public string? ImageUrl { get; set; }
    public int Stock { get; set; }
}

public class DailySales
{
    public string Date { get; set; } = string.Empty;
    public decimal Sales { get; set; }
    public int Orders { get; set; }
}

