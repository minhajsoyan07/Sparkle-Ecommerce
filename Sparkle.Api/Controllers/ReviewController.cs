using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Api.Services;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Reviews;
using Sparkle.Infrastructure;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Sparkle.Api.Controllers;

/// <summary>
/// Controller for product reviews with strict order verification.
/// Users can only review products they have purchased and received.
/// </summary>
[Authorize]
public class ReviewController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IReviewService _reviewService;
    private readonly ILogger<ReviewController> _logger;
    private readonly IWebHostEnvironment _env;

    public ReviewController(
        ApplicationDbContext db,
        IReviewService reviewService,
        ILogger<ReviewController> logger,
        IWebHostEnvironment env)
    {
        _db = db;
        _reviewService = reviewService;
        _logger = logger;
        _env = env;
    }

    private string GetUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    private bool WantsJson()
    {
        var accept = Request.Headers.Accept.ToString();
        return string.Equals(Request.Headers["X-Requested-With"].ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
            || accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// List all products the user can review (delivered but not yet reviewed).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var userId = GetUserId();
        var reviewableItems = await _reviewService.GetReviewableOrderItemsAsync(userId);
        var submittedReviews = await _db.ProductReviews
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Product)
                .ThenInclude(p => p.Images)
            .Include(r => r.Product)
                .ThenInclude(p => p.Seller)
            .Include(r => r.Images)
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.ReviewDate)
            .ThenByDescending(r => r.Id)
            .ToListAsync();

        return View(new ReviewDashboardViewModel
        {
            PendingItems = reviewableItems,
            SubmittedReviews = submittedReviews
        });
    }

    /// <summary>
    /// Check if user can review a specific product.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> CanReview(int productId)
    {
        var userId = GetUserId();
        var eligibility = await _reviewService.CheckEligibilityAsync(userId, productId);
        return Json(eligibility);
    }

    /// <summary>
    /// Show review form for a product.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create(int productId, int? orderItemId)
    {
        var userId = GetUserId();
        
        // Verify eligibility
        var eligibility = await _reviewService.CheckEligibilityAsync(userId, productId);
        if (!eligibility.CanReview)
        {
            TempData["Error"] = eligibility.Reason;
            return RedirectToAction("Index", "Profile");
        }

        var product = await _db.Products
            .Include(p => p.Images)
            .Include(p => p.Seller)
            .FirstOrDefaultAsync(p => p.Id == productId);

        if (product == null)
            return NotFound();

        return Redirect($"/home/product/{productId}?writeReview=1#reviews");
    }

    /// <summary>
    /// Submit a product review.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        int productId,
        int rating,
        string? title,
        string? comment,
        int? qualityRating,
        int? valueForMoneyRating,
        int? accuracyRating,
        List<IFormFile>? images)
    {
        var userId = GetUserId();

        // Handle image uploads
        var imageUrls = new List<string>();
        if (images?.Any() == true)
        {
            var uploadsFolder = GetReviewUploadsFolder();
            Directory.CreateDirectory(uploadsFolder);

            foreach (var image in images.Take(5))
            {
                if (image.Length > 0 && image.Length <= 5 * 1024 * 1024) // Max 5MB
                {
                    var fileName = $"{Guid.NewGuid()}{Path.GetExtension(image.FileName)}";
                    var filePath = Path.Combine(uploadsFolder, fileName);
                    
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await image.CopyToAsync(stream);
                    }
                    
                    imageUrls.Add($"/uploads/reviews/{fileName}");
                }
            }
        }

        var request = new ReviewSubmissionRequest
        {
            UserId = userId,
            ProductId = productId,
            Rating = rating,
            Title = title,
            Comment = comment,
            QualityRating = qualityRating,
            ValueForMoneyRating = valueForMoneyRating,
            AccuracyRating = accuracyRating,
            ImageUrls = imageUrls
        };

        var result = await _reviewService.SubmitReviewAsync(request);

        if (result.Success)
        {
            if (WantsJson())
            {
                return Json(new
                {
                    success = true,
                    message = result.Message,
                    reviewId = result.ReviewId
                });
            }

            TempData["Success"] = result.Message;
            return RedirectToAction("Index", "Profile", new { area = "" });
        }
        else
        {
            if (WantsJson())
            {
                Response.StatusCode = 400;
                return Json(new
                {
                    success = false,
                    message = result.Message
                });
            }

            TempData["Error"] = result.Message;
            return RedirectToAction("Create", new { productId });
        }
    }

    /// <summary>
    /// Get reviews for a product (public API).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> ProductReviews(int productId, int page = 1)
    {
        var reviews = await _reviewService.GetProductReviewsAsync(productId, page, 10);
        var stats = await _reviewService.GetProductReviewStatsAsync(productId);

        return Json(new
        {
            reviews = reviews.Select(r => new
            {
                r.Id,
                r.Rating,
                r.Title,
                r.Comment,
                ReviewerName = r.User?.FullName ?? "Customer",
                ReviewerAvatarUrl = r.User?.ProfilePhotoPath,
                ReviewDate = r.ReviewDate.ToString("MMM d, yyyy"),
                r.IsVerifiedPurchase,
                r.HelpfulCount,
                Images = r.Images.Select(i => i.ImageUrl).ToList(),
                SellerResponse = r.SellerResponse,
                SellerResponseDate = r.SellerResponseDate?.ToString("MMM d, yyyy")
            }),
            stats = new
            {
                stats.TotalReviews,
                stats.AverageRating,
                stats.FiveStarCount,
                stats.FourStarCount,
                stats.ThreeStarCount,
                stats.TwoStarCount,
                stats.OneStarCount
            }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var userId = GetUserId();
        var review = await _db.ProductReviews
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Product)
                .ThenInclude(p => p.Images)
            .Include(r => r.Product)
                .ThenInclude(p => p.Seller)
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (review == null)
            return NotFound();

        if (review.IsLocked)
        {
            TempData["Error"] = "This review is locked and cannot be edited.";
            return RedirectToAction(nameof(Index));
        }

        return View(new ReviewEditViewModel
        {
            Id = review.Id,
            ProductId = review.ProductId,
            ProductTitle = review.Product?.Title ?? "Product",
            ProductImage = review.Product?.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url,
            SellerName = review.Product?.Seller?.ShopName ?? "Sparkle Store",
            Rating = review.Rating,
            Title = review.Title,
            Comment = review.Comment,
            QualityRating = review.QualityRating,
            ValueForMoneyRating = review.ValueForMoneyRating,
            AccuracyRating = review.AccuracyRating,
            Status = review.Status,
            IsVerifiedPurchase = review.IsVerifiedPurchase,
            ReviewDate = review.ReviewDate,
            ExistingImages = review.Images
                .OrderBy(i => i.DisplayOrder)
                .Select(i => new ReviewImageViewModel { Id = i.Id, ImageUrl = i.ImageUrl })
                .ToList()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ReviewEditViewModel model, List<IFormFile>? images, List<int>? removeImageIds)
    {
        var userId = GetUserId();

        if (model.Rating < 1 || model.Rating > 5)
        {
            ModelState.AddModelError(nameof(model.Rating), "Select a rating between 1 and 5.");
        }

        if (!ModelState.IsValid)
        {
            await HydrateEditViewModelAsync(model, userId);
            return View(model);
        }

        var review = await _db.ProductReviews
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == model.Id && r.UserId == userId);

        if (review == null)
            return NotFound();

        if (review.IsLocked)
        {
            TempData["Error"] = "This review is locked and cannot be edited.";
            return RedirectToAction(nameof(Index));
        }

        var oldRating = review.Rating;
        var oldComment = review.Comment;
        var wasApproved = string.Equals(review.Status, "Approved", StringComparison.OrdinalIgnoreCase);

        _db.ReviewEditHistories.Add(new ReviewEditHistory
        {
            ProductReviewId = review.Id,
            PreviousRating = review.Rating,
            NewRating = model.Rating,
            PreviousComment = review.Comment,
            NewComment = model.Comment?.Trim(),
            EditedBy = userId,
            EditType = "Update",
            EditedAt = DateTime.UtcNow
        });

        review.Rating = model.Rating;
        review.Title = (model.Title ?? string.Empty).Trim();
        review.Comment = (model.Comment ?? string.Empty).Trim();
        review.QualityRating = model.QualityRating;
        review.ValueForMoneyRating = model.ValueForMoneyRating;
        review.AccuracyRating = model.AccuracyRating;
        review.LastEditedAt = DateTime.UtcNow;
        review.EditCount++;
        review.UpdatedAt = DateTime.UtcNow;
        review.UpdatedBy = userId;

        if (string.Equals(review.Status, "Rejected", StringComparison.OrdinalIgnoreCase))
        {
            review.Status = "Pending";
            review.RejectionReason = null;
        }

        if (removeImageIds?.Any() == true)
        {
            var imagesToRemove = review.Images
                .Where(i => removeImageIds.Contains(i.Id))
                .ToList();

            foreach (var image in imagesToRemove)
            {
                DeleteReviewImageFile(image.ImageUrl);
                _db.ReviewImages.Remove(image);
            }
        }

        if (images?.Any(i => i.Length > 0) == true)
        {
            var remainingSlots = Math.Max(0, 5 - review.Images.Count(i => removeImageIds?.Contains(i.Id) != true));
            var imageUrls = await SaveReviewImagesAsync(images, remainingSlots);
            var nextDisplayOrder = review.Images.Any() ? review.Images.Max(i => i.DisplayOrder) + 1 : 0;

            foreach (var imageUrl in imageUrls)
            {
                _db.ReviewImages.Add(new ReviewImage
                {
                    ProductReviewId = review.Id,
                    ImageUrl = imageUrl,
                    DisplayOrder = nextDisplayOrder++
                });
            }
        }

        await _db.SaveChangesAsync();

        if (wasApproved || string.Equals(review.Status, "Approved", StringComparison.OrdinalIgnoreCase) || oldRating != review.Rating || oldComment != review.Comment)
        {
            await RecalculateProductRatingAsync(review.ProductId);
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = "Review updated successfully.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = GetUserId();
        var review = await _db.ProductReviews
            .Include(r => r.Images)
            .Include(r => r.Votes)
            .Include(r => r.EditHistory)
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == userId);

        if (review == null)
        {
            TempData["Error"] = "Review not found.";
            return RedirectToAction(nameof(Index));
        }

        if (review.IsLocked)
        {
            TempData["Error"] = "This review is locked and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        var productId = review.ProductId;
        var orderItemId = review.OrderItemId;

        foreach (var image in review.Images.ToList())
        {
            DeleteReviewImageFile(image.ImageUrl);
        }

        if (review.Votes.Any()) _db.ReviewVotes.RemoveRange(review.Votes);
        if (review.Images.Any()) _db.ReviewImages.RemoveRange(review.Images);
        if (review.EditHistory.Any()) _db.ReviewEditHistories.RemoveRange(review.EditHistory);

        _db.ProductReviews.Remove(review);

        if (orderItemId.HasValue)
        {
            var orderItem = await _db.OrderItems
                .FirstOrDefaultAsync(i => i.Id == orderItemId.Value && i.Order.UserId == userId);

            if (orderItem != null)
            {
                orderItem.IsReviewed = false;
            }
        }

        await _db.SaveChangesAsync();
        await RecalculateProductRatingAsync(productId);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Review deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Vote on a review (helpful/not helpful).
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Vote(int reviewId, bool isHelpful)
    {
        var userId = GetUserId();
        
        // Check if already voted
        var existingVote = await _db.ReviewVotes
            .FirstOrDefaultAsync(v => v.ProductReviewId == reviewId && v.UserId == userId);

        if (existingVote != null)
        {
            return Json(new { success = false, message = "You have already voted on this review" });
        }

        var review = await _db.ProductReviews.FindAsync(reviewId);
        if (review == null)
            return NotFound();

        var vote = new Sparkle.Domain.Reviews.ReviewVote
        {
            ProductReviewId = reviewId,
            UserId = userId,
            IsHelpful = isHelpful,
            VotedAt = DateTime.UtcNow
        };

        _db.ReviewVotes.Add(vote);
        
        if (isHelpful)
            review.HelpfulCount++;
        else
            review.NotHelpfulCount++;

        await _db.SaveChangesAsync();

        return Json(new { success = true, helpfulCount = review.HelpfulCount });
    }

    /// <summary>
    /// Report a review.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Report(int reviewId, string reason)
    {
        var review = await _db.ProductReviews.FindAsync(reviewId);
        if (review == null)
            return NotFound();

        review.ReportCount++;
        
        // If report count exceeds threshold, flag for moderation
        if (review.ReportCount >= 3)
        {
            review.Status = "PendingModeration";
        }

        await _db.SaveChangesAsync();
        
        _logger.LogInformation("Review {ReviewId} reported by user. Reason: {Reason}", reviewId, reason);

        return Json(new { success = true, message = "Thank you for your report. We will review it shortly." });
    }

    private string GetReviewUploadsFolder()
    {
        var webRoot = _env.WebRootPath ?? Path.Combine(_env.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "uploads", "reviews");
    }

    private async Task<List<string>> SaveReviewImagesAsync(List<IFormFile> images, int maxImages)
    {
        var savedUrls = new List<string>();
        if (maxImages <= 0) return savedUrls;

        var uploadsFolder = GetReviewUploadsFolder();
        Directory.CreateDirectory(uploadsFolder);
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp", ".bmp"
        };

        foreach (var image in images.Where(i => i.Length > 0).Take(maxImages))
        {
            var ext = Path.GetExtension(image.FileName);
            if (!allowedExtensions.Contains(ext) || image.Length > 5 * 1024 * 1024)
                continue;

            var fileName = $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await image.CopyToAsync(stream);
            }

            savedUrls.Add($"/uploads/reviews/{fileName}");
        }

        return savedUrls;
    }

    private void DeleteReviewImageFile(string imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith("/uploads/reviews/", StringComparison.OrdinalIgnoreCase))
            return;

        var fileName = Path.GetFileName(imageUrl);
        if (string.IsNullOrWhiteSpace(fileName))
            return;

        var filePath = Path.Combine(GetReviewUploadsFolder(), fileName);
        if (System.IO.File.Exists(filePath))
        {
            System.IO.File.Delete(filePath);
        }
    }

    private async Task HydrateEditViewModelAsync(ReviewEditViewModel model, string userId)
    {
        var review = await _db.ProductReviews
            .AsNoTracking()
            .AsSplitQuery()
            .Include(r => r.Product)
                .ThenInclude(p => p.Images)
            .Include(r => r.Product)
                .ThenInclude(p => p.Seller)
            .Include(r => r.Images)
            .FirstOrDefaultAsync(r => r.Id == model.Id && r.UserId == userId);

        if (review == null) return;

        model.ProductId = review.ProductId;
        model.ProductTitle = review.Product?.Title ?? "Product";
        model.ProductImage = review.Product?.Images.OrderBy(i => i.SortOrder).FirstOrDefault()?.Url;
        model.SellerName = review.Product?.Seller?.ShopName ?? "Sparkle Store";
        model.Status = review.Status;
        model.IsVerifiedPurchase = review.IsVerifiedPurchase;
        model.ReviewDate = review.ReviewDate;
        model.ExistingImages = review.Images
            .OrderBy(i => i.DisplayOrder)
            .Select(i => new ReviewImageViewModel { Id = i.Id, ImageUrl = i.ImageUrl })
            .ToList();
    }

    private async Task RecalculateProductRatingAsync(int productId)
    {
        var ratings = await _db.ProductReviews
            .Where(r => r.ProductId == productId && r.Status == "Approved")
            .Select(r => r.Rating)
            .ToListAsync();

        var product = await _db.Products.FindAsync(productId);
        if (product == null) return;

        product.TotalReviews = ratings.Count;
        product.AverageRating = ratings.Count == 0 ? 0 : (decimal)ratings.Average();
        product.UpdatedAt = DateTime.UtcNow;
    }

    public class ReviewDashboardViewModel
    {
        public List<OrderItem> PendingItems { get; set; } = new();
        public List<ProductReview> SubmittedReviews { get; set; } = new();
    }

    public class ReviewEditViewModel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? ProductImage { get; set; }
        public string? SellerName { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [MaxLength(100)]
        public string? Title { get; set; }

        [MaxLength(1000)]
        public string? Comment { get; set; }

        [Range(1, 5)]
        public int? QualityRating { get; set; }

        [Range(1, 5)]
        public int? ValueForMoneyRating { get; set; }

        [Range(1, 5)]
        public int? AccuracyRating { get; set; }

        public string Status { get; set; } = string.Empty;
        public bool IsVerifiedPurchase { get; set; }
        public DateTime ReviewDate { get; set; }
        public List<ReviewImageViewModel> ExistingImages { get; set; } = new();
    }

    public class ReviewImageViewModel
    {
        public int Id { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
    }
}
