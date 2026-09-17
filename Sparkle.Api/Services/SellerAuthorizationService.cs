using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Services;

/// <summary>
/// Helper service for seller authorization and management operations
/// Ensures sellers can only manage their own products and stock
/// </summary>
public interface ISellerAuthorizationService
{
    /// <summary>
    /// Gets the seller ID for the current user
    /// </summary>
    Task<int?> GetCurrentSellerIdAsync(string userId);

    /// <summary>
    /// Verifies that the seller owns the product
    /// </summary>
    Task<bool> SellerOwnsProductAsync(int sellerId, int productId);

    /// <summary>
    /// Verifies that the seller owns the variant
    /// </summary>
    Task<bool> SellerOwnsVariantAsync(int sellerId, int variantId);

    /// <summary>
    /// Gets seller details by user ID
    /// </summary>
    Task<(int SellerId, string ShopName)?> GetSellerDetailsAsync(string userId);
}

public class SellerAuthorizationService : ISellerAuthorizationService
{
    private readonly ApplicationDbContext _db;
  private readonly ILogger<SellerAuthorizationService> _logger;

    public SellerAuthorizationService(ApplicationDbContext db, ILogger<SellerAuthorizationService> logger)
    {
        _db = db;
   _logger = logger;
    }

    /// <summary>
    /// Gets the seller ID for the current user
    /// </summary>
    public async Task<int?> GetCurrentSellerIdAsync(string userId)
  {
  if (string.IsNullOrEmpty(userId))
          return null;

        var seller = await _db.Sellers
        .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId);

    return seller?.Id;
    }

    /// <summary>
    /// Verifies that the seller owns the product
    /// </summary>
    public async Task<bool> SellerOwnsProductAsync(int sellerId, int productId)
    {
        var product = await _db.Products
         .AsNoTracking()
          .FirstOrDefaultAsync(p => p.Id == productId && p.SellerId == sellerId);

        return product != null;
    }

    /// <summary>
    /// Verifies that the seller owns the variant
    /// </summary>
    public async Task<bool> SellerOwnsVariantAsync(int sellerId, int variantId)
    {
        var variant = await _db.ProductVariants
      .AsNoTracking()
  .Include(v => v.Product)
   .FirstOrDefaultAsync(v => v.Id == variantId && v.Product.SellerId == sellerId);

      return variant != null;
    }

    /// <summary>
    /// Gets seller details by user ID
    /// </summary>
    public async Task<(int SellerId, string ShopName)?> GetSellerDetailsAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
  return null;

        var seller = await _db.Sellers
      .AsNoTracking()
      .Where(s => s.UserId == userId)
          .Select(s => new { s.Id, s.ShopName })
         .FirstOrDefaultAsync();

      if (seller == null)
return null;

        return (seller.Id, seller.ShopName);
    }
}
