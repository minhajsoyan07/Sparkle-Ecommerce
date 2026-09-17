using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Hubs;
using Sparkle.Domain.Support;
using Sparkle.Infrastructure;
using System.Security.Claims;

namespace Sparkle.Api.Controllers.Api;

/// <summary>
/// API controller for chat functionality with product suggestions
/// </summary>
[ApiController]
[Route("api/chat")]
[Authorize]
public class ChatApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatApiController> _logger;

    public ChatApiController(
        ApplicationDbContext db,
        IHubContext<ChatHub> hubContext,
        ILogger<ChatApiController> logger)
    {
        _db = db;
        _hubContext = hubContext;
        _logger = logger;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    /// <summary>
    /// Search products from a seller by name for auto-suggest in chat
    /// </summary>
    [HttpGet("seller/{sellerId}/search-products")]
    public async Task<IActionResult> SearchSellerProducts(int sellerId, [FromQuery] string query, [FromQuery] int limit = 5)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest(new { success = false, message = "Search query required" });

            var seller = await _db.Sellers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sellerId);

            if (seller == null)
                return NotFound(new { success = false, message = "Seller not found" });

            var searchTerm = query.ToLower().Trim();
            
            var products = await _db.Products
                .AsNoTracking()
                .Where(p => p.SellerId == sellerId && 
                           p.IsActive && 
                           p.ModerationStatus.ToString() == "Approved" &&
                           (p.Title != null && p.Title.ToLower().Contains(searchTerm) ||
                            p.ShortDescription != null && p.ShortDescription.ToLower().Contains(searchTerm)))
                .OrderByDescending(p => p.Title != null && p.Title.ToLower().StartsWith(searchTerm))
                .ThenByDescending(p => p.PurchaseCount)
                .Take(limit)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Slug,
                    p.BasePrice,
                    Price = p.DiscountPercent.HasValue 
                        ? p.BasePrice * (1 - (p.DiscountPercent.Value / 100m))
                        : p.BasePrice,
                    Discount = p.DiscountPercent ?? 0,
                    ThumbnailUrl = p.Images != null && p.Images.Any() ? p.Images.FirstOrDefault()!.Url : null,
                    p.AverageRating,
                    p.TotalReviews,
                    Stock = p.Variants != null ? p.Variants.Sum(v => v.Stock) : 0
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = products,
                count = products.Count,
                seller = new { seller.Id, seller.ShopName }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching seller products");
            return BadRequest(new { success = false, message = "Error searching products" });
        }
    }

    /// <summary>
    /// Get all active products from a specific seller for suggestion in chat
    /// </summary>
    [HttpGet("seller/{sellerId}/products")]
    public async Task<IActionResult> GetSellerProducts(int sellerId, [FromQuery] int limit = 20)
    {
        try
        {
            var seller = await _db.Sellers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sellerId);

            if (seller == null)
                return NotFound(new { success = false, message = "Seller not found" });

            var products = await _db.Products
                .AsNoTracking()
                .Where(p => p.SellerId == sellerId && p.IsActive && p.ModerationStatus.ToString() == "Approved")
                .OrderByDescending(p => p.CreatedAt)
                .Take(limit)
                .Select(p => new
                {
                    p.Id,
                    p.Title,
                    p.Slug,
                    p.BasePrice,
                    Price = p.DiscountPercent.HasValue 
                        ? p.BasePrice * (1 - (p.DiscountPercent.Value / 100m))
                        : p.BasePrice,
                    Discount = p.DiscountPercent ?? 0,
                    ThumbnailUrl = p.Images != null && p.Images.FirstOrDefault() != null ? p.Images.FirstOrDefault()!.Url : null,
                    p.AverageRating,
                    p.TotalReviews,
                    Stock = p.Variants != null ? p.Variants.Sum(v => v.Stock) : 0
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = products,
                count = products.Count,
                seller = new { seller.Id, seller.ShopName }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching seller products");
            return BadRequest(new { success = false, message = "Error fetching products" });
        }
    }

    /// <summary>
    /// Suggest a product from the seller in a chat conversation
    /// </summary>
    [HttpPost("{chatId}/suggest-product")]
    public async Task<IActionResult> SuggestProduct(int chatId, [FromBody] SuggestProductRequest request)
    {
        try
        {
            if (request == null || request.ProductId <= 0)
                return BadRequest(new { success = false, message = "Invalid product request" });

            var userId = GetUserId();

            // Verify chat exists and user is authorized
            var chat = await _db.Chats
                .Include(c => c.Seller)
                .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId);

            if (chat == null)
                return NotFound(new { success = false, message = "Chat not found" });

            // Verify product belongs to the seller in this chat
            var product = await _db.Products
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.SellerId == chat!.SellerId);

            if (product == null)
                return BadRequest(new { success = false, message = "Product not found or doesn't belong to this seller" });

            var productTitle = product.Title ?? "Product";
            var productImageUrl = product.Images?.FirstOrDefault()?.Url;

            var message = new ChatMessage
            {
                ChatId = chatId,
                SenderId = userId,
                IsSeller = false,
                Content = $"Suggested product: {productTitle}",
                MessageType = "ProductSuggestion",
                AttachmentUrl = productImageUrl,
                AttachmentName = request.Message ?? productTitle,
                SentAt = DateTime.UtcNow
            };

            _db.ChatMessages.Add(message);
            
            chat.LastMessageAt = DateTime.UtcNow;
            chat.SellerUnreadCount++;

            await _db.SaveChangesAsync();

            // Notify seller via SignalR with product details
            var user = await _db.Users.FindAsync(userId);
            var senderName = user?.FullName ?? "Customer";
            
            await _hubContext.Clients.Group($"seller_{chat.SellerId}").SendAsync("ProductSuggested", new
            {
                ChatId = chatId,
                SenderName = senderName,
                ProductId = product!.Id,
                ProductTitle = product!.Title ?? "Product",
                ProductUrl = $"/product/{product!.Slug ?? string.Empty}",
                ProductImage = productImageUrl,
                Message = request.Message ?? "Interested in this product",
                SuggestedAt = message.SentAt.ToString("o")
            });

            return Ok(new
            {
                success = true,
                message = "Product suggested successfully",
                messageId = message.Id
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error suggesting product");
            return BadRequest(new { success = false, message = "Error suggesting product" });
        }
    }

    /// <summary>
    /// Get suggested products in a chat
    /// </summary>
    [HttpGet("{chatId}/suggested-products")]
    public async Task<IActionResult> GetSuggestedProducts(int chatId)
    {
        try
        {
            var userId = GetUserId();

            // Verify user is part of this chat
            var chat = await _db.Chats
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == chatId && c.UserId == userId);

            if (chat == null)
                return NotFound(new { success = false, message = "Chat not found" });

            // Get all product suggestions from this chat
            var suggestedProducts = await _db.ChatMessages
                .AsNoTracking()
                .Where(m => m.ChatId == chatId && m.MessageType == "ProductSuggestion")
                .OrderByDescending(m => m.SentAt)
                .Select(m => new
                {
                    m.Id,
                    m.AttachmentName,
                    m.AttachmentUrl,
                    SuggestedBy = m.IsSeller ? "Seller" : "You",
                    m.SentAt,
                    m.Content
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                data = suggestedProducts,
                count = suggestedProducts.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching suggested products");
            return BadRequest(new { success = false, message = "Error fetching suggestions" });
        }
    }
}

/// <summary>
/// Request model for suggesting a product
/// </summary>
public class SuggestProductRequest
{
    public int ProductId { get; set; }
    public string? Message { get; set; }
}
