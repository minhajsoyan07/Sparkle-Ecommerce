using Microsoft.EntityFrameworkCore;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Reviews;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Services;

/// <summary>
/// Service for handling product reviews with strict order verification.
/// Ensures users can only review products they have purchased.
/// </summary>
public interface IReviewService
{
    /// <summary>
    /// Check if a user is eligible to review a specific product.
    /// </summary>
    Task<ReviewEligibility> CheckEligibilityAsync(string userId, int productId);
    
    /// <summary>
    /// Get all reviewable order items for a user (delivered orders without reviews).
    /// </summary>
    Task<List<OrderItem>> GetReviewableOrderItemsAsync(string userId);
    
    /// <summary>
    /// Submit a product review with order verification.
    /// </summary>
    Task<ReviewSubmissionResult> SubmitReviewAsync(ReviewSubmissionRequest request);
    
    /// <summary>
    /// Get reviews for a product.
    /// </summary>
    Task<List<ProductReview>> GetProductReviewsAsync(int productId, int page = 1, int pageSize = 10);
    
    /// <summary>
    /// Get review statistics for a product.
    /// </summary>
    Task<ReviewStatistics> GetProductReviewStatsAsync(int productId);
    
    /// <summary>
    /// Add seller response to a review.
    /// </summary>
    Task<bool> AddSellerResponseAsync(int sellerId, int reviewId, string response);
}

public class ReviewService : IReviewService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(ApplicationDbContext db, ILogger<ReviewService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<ReviewEligibility> CheckEligibilityAsync(string userId, int productId)
    {
        // Find if user has any delivered orders containing this product
        var eligibleOrderItem = await _db.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .Where(oi => 
                oi.Order.UserId == userId &&
                oi.ProductId == productId &&
                (oi.Order.Status == OrderStatus.Delivered || oi.ItemStatus == OrderStatus.Delivered) &&
                !oi.IsReviewed)
            .FirstOrDefaultAsync();

        if (eligibleOrderItem == null)
        {
            // Check if user has ordered but not yet delivered
            var pendingOrder = await _db.OrderItems
                .Include(oi => oi.Order)
                .AnyAsync(oi => 
                    oi.Order.UserId == userId &&
                    oi.ProductId == productId &&
                    oi.Order.Status != OrderStatus.Delivered &&
                    oi.Order.Status != OrderStatus.Cancelled);

            if (pendingOrder)
            {
                return new ReviewEligibility
                {
                    CanReview = false,
                    Reason = "Order not yet delivered. You can review after receiving your order."
                };
            }

            // Check if already reviewed
            var alreadyReviewed = await _db.ProductReviews
                .AnyAsync(r => r.UserId == userId && r.ProductId == productId);

            if (alreadyReviewed)
            {
                return new ReviewEligibility
                {
                    CanReview = false,
                    Reason = "You have already reviewed this product."
                };
            }

            return new ReviewEligibility
            {
                CanReview = false,
                Reason = "You must purchase and receive this product before reviewing."
            };
        }

        return new ReviewEligibility
        {
            CanReview = true,
            OrderItemId = eligibleOrderItem.Id,
            OrderId = eligibleOrderItem.OrderId,
            PurchaseDate = eligibleOrderItem.Order.OrderDate,
            ProductName = eligibleOrderItem.ProductName
        };
    }

    public async Task<List<OrderItem>> GetReviewableOrderItemsAsync(string userId)
    {
        return await _db.OrderItems
            .Include(oi => oi.Order)
            .Include(oi => oi.Product)
            .ThenInclude(p => p.Images)
            .Where(oi => 
                oi.Order.UserId == userId &&
                (oi.Order.Status == OrderStatus.Delivered || oi.ItemStatus == OrderStatus.Delivered) &&
                !oi.IsReviewed)
            .OrderByDescending(oi => oi.Order.DeliveredAt ?? oi.Order.OrderDate)
            .ToListAsync();
    }

    public async Task<ReviewSubmissionResult> SubmitReviewAsync(ReviewSubmissionRequest request)
    {
        try
        {
            // Validate eligibility
            var eligibility = await CheckEligibilityAsync(request.UserId, request.ProductId);
            if (!eligibility.CanReview)
            {
                return new ReviewSubmissionResult
                {
                    Success = false,
                    Message = eligibility.Reason ?? "You are not eligible to review this product."
                };
            }

            // Validate rating
            if (request.Rating < 1 || request.Rating > 5)
            {
                return new ReviewSubmissionResult
                {
                    Success = false,
                    Message = "Rating must be between 1 and 5 stars."
                };
            }

            // Check for duplicate review (double-check)
            var existingReview = await _db.ProductReviews
                .AnyAsync(r => r.UserId == request.UserId && r.ProductId == request.ProductId);
            
            if (existingReview)
            {
                return new ReviewSubmissionResult
                {
                    Success = false,
                    Message = "You have already reviewed this product."
                };
            }

            // Get the product to find the seller
            var product = await _db.Products.FindAsync(request.ProductId);
            if (product == null)
            {
                return new ReviewSubmissionResult
                {
                    Success = false,
                    Message = "Product not found."
                };
            }

            // Create the review
            var review = new ProductReview
            {
                ProductId = request.ProductId,
                UserId = request.UserId,
                OrderItemId = eligibility.OrderItemId,
                SellerId = product.SellerId,
                Rating = request.Rating,
                Title = request.Title?.Trim() ?? "",
                Comment = request.Comment?.Trim() ?? "",
                QualityRating = request.QualityRating,
                ValueForMoneyRating = request.ValueForMoneyRating,
                AccuracyRating = request.AccuracyRating,
                IsVerifiedPurchase = true,
                PurchaseDate = eligibility.PurchaseDate,
                Status = "Approved", // Auto-approve verified purchases
                ApprovedAt = DateTime.UtcNow,
                ReviewDate = DateTime.UtcNow
            };

            _db.ProductReviews.Add(review);

            // Add review images
            if (request.ImageUrls?.Any() == true)
            {
                var maxImages = Math.Min(request.ImageUrls.Count, 5);
                for (int i = 0; i < maxImages; i++)
                {
                    _db.ReviewImages.Add(new ReviewImage
                    {
                        ProductReview = review,
                        ImageUrl = request.ImageUrls[i],
                        DisplayOrder = i
                    });
                }
            }

            // Mark order item as reviewed
            if (eligibility.OrderItemId.HasValue)
            {
                var orderItem = await _db.OrderItems.FindAsync(eligibility.OrderItemId.Value);
                if (orderItem != null)
                {
                    orderItem.IsReviewed = true;
                }
            }

            await _db.SaveChangesAsync();
            
            // Update product statistics after the review exists in the database.
            await UpdateProductRatingAsync(request.ProductId);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Review submitted for product {ProductId} by user {UserId}", 
                request.ProductId, request.UserId);

            return new ReviewSubmissionResult
            {
                Success = true,
                Message = "Thank you! Your review has been submitted successfully.",
                ReviewId = review.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting review for product {ProductId}", request.ProductId);
            return new ReviewSubmissionResult
            {
                Success = false,
                Message = "An error occurred while submitting your review. Please try again."
            };
        }
    }

    public async Task<List<ProductReview>> GetProductReviewsAsync(int productId, int page = 1, int pageSize = 10)
    {
        return await _db.ProductReviews
            .AsNoTracking()
            .Include(r => r.User)
            .Include(r => r.Images)
            .Where(r => r.ProductId == productId && r.Status == "Approved")
            .OrderByDescending(r => r.ReviewDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<ReviewStatistics> GetProductReviewStatsAsync(int productId)
    {
        var reviews = await _db.ProductReviews
            .Where(r => r.ProductId == productId && r.Status == "Approved")
            .Select(r => r.Rating)
            .ToListAsync();

        if (!reviews.Any())
        {
            return new ReviewStatistics { ProductId = productId };
        }

        return new ReviewStatistics
        {
            ProductId = productId,
            TotalReviews = reviews.Count,
            AverageRating = (decimal)reviews.Average(),
            FiveStarCount = reviews.Count(r => r == 5),
            FourStarCount = reviews.Count(r => r == 4),
            ThreeStarCount = reviews.Count(r => r == 3),
            TwoStarCount = reviews.Count(r => r == 2),
            OneStarCount = reviews.Count(r => r == 1)
        };
    }

    public async Task<bool> AddSellerResponseAsync(int sellerId, int reviewId, string response)
    {
        var review = await _db.ProductReviews
            .Include(r => r.Product)
            .FirstOrDefaultAsync(r => r.Id == reviewId && r.Product.SellerId == sellerId);

        if (review == null)
            return false;

        review.SellerResponse = response.Trim();
        review.SellerResponseDate = DateTime.UtcNow;
        
        await _db.SaveChangesAsync();
        return true;
    }

    private async Task UpdateProductRatingAsync(int productId)
    {
        var stats = await GetProductReviewStatsAsync(productId);
        var product = await _db.Products.FindAsync(productId);
        
        if (product != null && stats.TotalReviews > 0)
        {
            product.AverageRating = stats.AverageRating;
            product.TotalReviews = stats.TotalReviews;
        }
    }
}

#region DTOs

public class ReviewEligibility
{
    public bool CanReview { get; set; }
    public string? Reason { get; set; }
    public int? OrderItemId { get; set; }
    public int? OrderId { get; set; }
    public DateTime? PurchaseDate { get; set; }
    public string? ProductName { get; set; }
}

public class ReviewSubmissionRequest
{
    public string UserId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public int Rating { get; set; }
    public string? Title { get; set; }
    public string? Comment { get; set; }
    public int? QualityRating { get; set; }
    public int? ValueForMoneyRating { get; set; }
    public int? AccuracyRating { get; set; }
    public List<string>? ImageUrls { get; set; }
}

public class ReviewSubmissionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ReviewId { get; set; }
}

public class ReviewStatistics
{
    public int ProductId { get; set; }
    public int TotalReviews { get; set; }
    public decimal AverageRating { get; set; }
    public int FiveStarCount { get; set; }
    public int FourStarCount { get; set; }
    public int ThreeStarCount { get; set; }
    public int TwoStarCount { get; set; }
    public int OneStarCount { get; set; }
}

#endregion
