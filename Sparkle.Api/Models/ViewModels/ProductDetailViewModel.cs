using Sparkle.Domain.Catalog;
using Sparkle.Domain.Sellers;

namespace Sparkle.Api.Models.ViewModels;

public class ProductDetailViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ShortDescription { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    
    // Pricing
    public decimal BasePrice { get; set; }
    public decimal? DiscountPercent { get; set; }
    
    // Stats
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int PurchaseCount { get; set; } // "Sold" count
    
    // Relations
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    
    public int SellerId { get; set; }
    public string SellerName { get; set; } = string.Empty;
    public double SellerRating { get; set; }
    public int SellerFollowers { get; set; } // Placeholder for now
    public bool IsSellerActive { get; set; }

    // Specs
    public string BrandName { get; set; } = "Generic";
    public string Dimensions { get; set; } = string.Empty;
    public string Weight { get; set; } = string.Empty;
    public string Features { get; set; } = string.Empty; // Assuming JSON or string for now
    
    // Media
    public List<string> Images { get; set; } = new();
    
    // Variants
    public List<ProductVariantViewModel> Variants { get; set; } = new();

    // User Specific
    public bool IsWishlisted { get; set; }
    public bool IsInCart { get; set; }
    
    // Computed
    public int TotalStock => Variants.Sum(v => v.Stock);
    public decimal FinalPrice => DiscountPercent.HasValue && DiscountPercent > 0 
        ? Math.Round(BasePrice * (1 - (DiscountPercent.Value / 100m))) 
        : BasePrice;
}

public class ProductVariantViewModel
{
    public int Id { get; set; }
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    
    // Helper to check if this variant has discount
    public decimal CalculatedPrice(decimal? discountPercent)
    {
        if (!discountPercent.HasValue || discountPercent <= 0) return Price;
        return Math.Round(Price * (1 - (discountPercent.Value / 100m)));
    }
}
