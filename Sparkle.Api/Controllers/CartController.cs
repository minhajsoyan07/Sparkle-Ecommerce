using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Catalog;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Controllers;

// [Authorize(Roles = "User")] // Allowed anonymous for guest cart
[Route("cart")]
public class CartController : Controller
{
    private readonly ApplicationDbContext _db;

    public CartController(ApplicationDbContext db)
    {
        _db = db;
    }

    private string GetCartUserId()
    {
        if (User?.Identity?.IsAuthenticated == true)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
                ?? User.FindFirstValue(ClaimTypes.Name);
            return userId ?? throw new InvalidOperationException("User found but no ID");
        }

        // Guest Logic: Use Persistent Cookie
        const string cookieName = "Sparkle_GuestCartId";
        if (Request.Cookies.TryGetValue(cookieName, out var cookieValue) && !string.IsNullOrEmpty(cookieValue))
        {
            return cookieValue;
        }

        // Generate new Guest ID
        var newGuestId = $"guest_{Guid.NewGuid()}";
        
        var options = new CookieOptions
        {
            Expires = DateTime.UtcNow.AddDays(30),
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        };
        Response.Cookies.Append(cookieName, newGuestId, options);
        return newGuestId;
    }

    [HttpGet("")]
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        try
        {
            if (User.IsInRole("Seller"))
            {
                TempData["Error"] = "Sellers cannot place orders.";
                return Redirect("/seller/dashboard");
            }

            var userId = GetCartUserId();
            var cart = await _db.Carts
                .AsSplitQuery()
                .Include(c => c.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(v => v.Product)
                            .ThenInclude(p => p.Images)
                 .Include(c => c.Items)
                    .ThenInclude(i => i.ProductVariant)
                        .ThenInclude(v => v.Product)
                            .ThenInclude(p => p.Seller)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart { UserId = userId, Items = new List<CartItem>() };
            }

            // Clean invalid items (Self-healing)
            var itemsToRemove = cart.Items
                .Where(i => i.ProductVariant == null || i.ProductVariant.Product == null)
                .ToList();

            if (itemsToRemove.Any())
            {
                _db.CartItems.RemoveRange(itemsToRemove);
                await _db.SaveChangesAsync();
            }

            var vm = new CartViewModel(cart);
            return View(vm);
        }
        catch (Exception)
        {
            // _logger.LogError(ex, "Error loading cart");
            TempData["Error"] = "Unable to load cart. Please try again.";
            return RedirectToAction("Index", "Home");
        }
    }

    [HttpGet("count")]
    [AllowAnonymous]
    public async Task<IActionResult> Count()
    {
        var userId = GetCartUserId();
        var count = await _db.CartItems
            .AsNoTracking()
            .Where(i => i.Cart.UserId == userId)
            .CountAsync();

        return Json(new { count });
    }

    [HttpPost("add")]
    [AllowAnonymous]
    public async Task<IActionResult> Add(int productVariantId, int quantity = 1, string? returnUrl = null)
    {
        try
        {
            if (User.IsInRole("Seller"))
            {
                TempData["Error"] = "Sellers cannot place orders.";
                return Redirect("/seller/dashboard");
            }

            if (quantity <= 0) quantity = 1;
            if (quantity > 99) quantity = 99;

            var variant = await _db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == productVariantId);
                
            if (variant == null || !variant.Product.IsActive)
            {
                TempData["Error"] = "Product is not available";
                return Redirect(returnUrl ?? "/");
            }

            if (variant.Stock < quantity)
            {
                TempData["Error"] = $"Only {variant.Stock} items available in stock";
                return Redirect(returnUrl ?? $"/Product/{variant.Product.Id}");
            }

            var userId = GetCartUserId();
            var cart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);
                
            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _db.Carts.Add(cart);
                // Save immediately to avoid race conditions with simultaneous requests
                await _db.SaveChangesAsync();
            }

            var existing = cart.Items.FirstOrDefault(i => i.ProductVariantId == productVariantId);
            if (existing != null)
            {
                var newQuantity = existing.Quantity + quantity;
                if (newQuantity > variant.Stock)
                {
                    TempData["Error"] = $"Cannot add more. Only {variant.Stock} items available";
                    return Redirect(returnUrl ?? "/cart");
                }
                existing.Quantity = newQuantity;
            }
            else
            {
                var finalPrice = variant.Price;
                if (variant.Product.DiscountPercent.HasValue && variant.Product.DiscountPercent.Value > 0)
                {
                    finalPrice = Math.Round(variant.Price * (1 - (variant.Product.DiscountPercent.Value / 100m)));
                }

                cart.Items.Add(new CartItem
                {
                    ProductVariantId = productVariantId,
                    Quantity = quantity,
                    UnitPrice = finalPrice
                });
            }

            await _db.SaveChangesAsync();
            
            TempData["Success"] = "Product added to cart successfully!";
            return Redirect(returnUrl ?? "/cart");
        }
        catch (Exception)
        {
            TempData["Error"] = "Unable to add product to cart. Please try again.";
            return Redirect(returnUrl ?? "/");
        }
    }

    /// <summary>
    /// AJAX-based Add to Cart endpoint - returns JSON instead of redirect
    /// </summary>
    [HttpPost("add-ajax")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> AddAjax([FromBody] AddToCartRequest? request)
    {
        try
        {
            if (User.IsInRole("Seller"))
            {
                return Json(new { success = false, message = "Sellers cannot place orders." });
            }

            if (request == null || request.ProductVariantId <= 0)
            {
                return Json(new AddToCartResponse 
                { 
                    Success = false, 
                    Message = "Invalid request. Please try again.",
                    CartCount = 0
                });
            }

            if (request.Quantity <= 0) request.Quantity = 1;
            if (request.Quantity > 99) request.Quantity = 99;

            var variant = await _db.ProductVariants
                .Include(v => v.Product)
                .FirstOrDefaultAsync(v => v.Id == request.ProductVariantId);
                
            if (variant == null || !variant.Product.IsActive)
            {
                return Json(new AddToCartResponse 
                { 
                    Success = false, 
                    Message = "Product is not available",
                    CartCount = await GetCartCount()
                });
            }

            if (variant.Stock < request.Quantity)
            {
                return Json(new AddToCartResponse 
                { 
                    Success = false, 
                    Message = $"Only {variant.Stock} items available in stock",
                    CartCount = await GetCartCount()
                });
            }

            var userId = GetCartUserId();
            var cart = await _db.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);
                
            if (cart == null)
            {
                cart = new Cart { UserId = userId };
                _db.Carts.Add(cart);
                // Save immediately to avoid race conditions with simultaneous requests
                await _db.SaveChangesAsync();
            }

            var existing = cart.Items.FirstOrDefault(i => i.ProductVariantId == request.ProductVariantId);
            if (existing != null)
            {
                var newQuantity = existing.Quantity + request.Quantity;
                if (newQuantity > variant.Stock)
                {
                    return Json(new AddToCartResponse 
                    { 
                        Success = false, 
                        Message = $"Cannot add more. Only {variant.Stock} items available",
                        CartCount = await GetCartCount()
                    });
                }
                existing.Quantity = newQuantity;
            }
            else
            {
                var finalPrice = variant.Price;
                if (variant.Product.DiscountPercent.HasValue && variant.Product.DiscountPercent.Value > 0)
                {
                    finalPrice = Math.Round(variant.Price * (1 - (variant.Product.DiscountPercent.Value / 100m)));
                }

                cart.Items.Add(new CartItem
                {
                    ProductVariantId = request.ProductVariantId,
                    Quantity = request.Quantity,
                    UnitPrice = finalPrice
                });
            }

            await _db.SaveChangesAsync();
            
            return Json(new AddToCartResponse 
            { 
                Success = true, 
                Message = "Product added to cart successfully!",
                CartCount = await GetCartCount(),
                ProductName = variant.Product.Title
            });
        }
        catch (Exception)
        {
            return Json(new AddToCartResponse 
            { 
                Success = false, 
                Message = "Unable to add product to cart. Please try again.",
                CartCount = await GetCartCount()
            });
        }
    }

    private async Task<int> GetCartCount()
    {
        var userId = GetCartUserId();
        return await _db.CartItems
            .AsNoTracking()
            .CountAsync(i => i.Cart.UserId == userId);
    }

    [HttpPost("update")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Update(int itemId, int quantity)
    {
        var userId = GetCartUserId();
        var cart = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart == null) return RedirectToAction(nameof(Index));

        var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
        // Fallback: match by ID even if cart logic is strict? 
        // With DB logic, itemId is PK.
        
        if (item != null)
        {
            if (quantity <= 0) 
            {
                cart.Items.Remove(item);
            }
            else 
            {
                // Check stock
                var variant = await _db.ProductVariants.FindAsync(item.ProductVariantId);
                if (variant != null && quantity <= variant.Stock)
                {
                    item.Quantity = quantity;
                }
                else if (variant != null)
                {
                     TempData["Error"] = $"Only {variant.Stock} items available";
                     item.Quantity = variant.Stock;
                }
            }
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("remove")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Remove(int itemId)
    {
        var userId = GetCartUserId();
        var cart = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart != null)
        {
            var item = cart.Items.FirstOrDefault(i => i.Id == itemId);
            if (item != null)
            {
                cart.Items.Remove(item);
                await _db.SaveChangesAsync();
            }
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("clear")]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Clear()
    {
        var userId = GetCartUserId();
        var cart = await _db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId);
        if (cart != null && cart.Items.Any())
        {
            _db.CartItems.RemoveRange(cart.Items);
            await _db.SaveChangesAsync();
            TempData["Success"] = "Cart cleared successfully.";
        }
        return RedirectToAction(nameof(Index));
    }

    public class CartViewModel
    {
        public CartViewModel(Cart cart)
        {
            Cart = cart;
        }

        public Cart Cart { get; }
        public decimal Subtotal => Cart.Items.Sum(i => i.UnitPrice * i.Quantity);
    }

    public class AddToCartRequest
    {
        public int ProductVariantId { get; set; }
        public int Quantity { get; set; } = 1;
    }

    public class AddToCartResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public int CartCount { get; set; }
        public string? ProductName { get; set; }
    }
}
