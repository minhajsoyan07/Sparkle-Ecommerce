using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Sparkle.Domain.Marketing;
using Sparkle.Domain.Users;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Sparkle.Infrastructure.Services
{
    public class LoyaltyService : ILoyaltyService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoyaltyService> _logger;

        public LoyaltyService(ApplicationDbContext context, ILogger<LoyaltyService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<int> GetUserPointBalanceAsync(string userId)
        {
            var profile = await _context.UserProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            return profile?.LoyaltyPoints ?? 0;
        }

        public async Task EarnPointsAsync(string userId, Guid orderId, decimal orderAmount)
        {
            var config = await GetConfigAsync();
            var pointsToEarn = (int)(orderAmount * (config.EarnRatePercentage / 100));

            if (pointsToEarn <= 0) return;

            var history = new LoyaltyPointHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrderId = orderId,
                Points = pointsToEarn,
                Type = PointTransactionType.Earned,
                Description = $"Earned from Order {orderId}",
                CreatedAt = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddDays(config.PointsExpiryDays)
            };

            _context.LoyaltyPointHistories.Add(history);

            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile != null)
            {
                profile.LoyaltyPoints += pointsToEarn;
                // Simple Tier Logic
                if (profile.LoyaltyPoints > 5000) profile.MembershipTier = "Gold";
                else if (profile.LoyaltyPoints > 1000) profile.MembershipTier = "Silver";
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation($"User {userId} earned {pointsToEarn} points for Order {orderId}");
        }

        public async Task<bool> RedeemPointsAsync(string userId, Guid orderId, int pointsToRedeem)
        {
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null || profile.LoyaltyPoints < pointsToRedeem)
            {
                return false;
            }

            var config = await GetConfigAsync();
            if (pointsToRedeem < config.MinPointsToRedeem)
            {
                return false;
            }

            var history = new LoyaltyPointHistory
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                OrderId = orderId,
                Points = -pointsToRedeem,
                Type = PointTransactionType.Redeemed,
                Description = $"Redeemed on Order {orderId}",
                CreatedAt = DateTime.UtcNow
            };

            _context.LoyaltyPointHistories.Add(history);
            profile.LoyaltyPoints -= pointsToRedeem;

            await _context.SaveChangesAsync();
            _logger.LogInformation($"User {userId} redeemed {pointsToRedeem} points on Order {orderId}");
            return true;
        }

        public async Task<decimal> CalculateDiscountValueAsync(int points)
        {
            var config = await GetConfigAsync();
            return points * config.RedemptionConversionRate;
        }

        public async Task RefundPointsAsync(string userId, Guid orderId)
        {
            // Find points earned from this order
            var earned = await _context.LoyaltyPointHistories
                .FirstOrDefaultAsync(h => h.UserId == userId && h.OrderId == orderId && h.Type == PointTransactionType.Earned);

            if (earned != null)
            {
                // Reverse earn
                var reversal = new LoyaltyPointHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrderId = orderId,
                    Points = -earned.Points,
                    Type = PointTransactionType.Adjusted,
                    Description = $"Reversal for refunded Order {orderId}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.LoyaltyPointHistories.Add(reversal);
                
                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile != null)
                {
                    profile.LoyaltyPoints -= earned.Points;
                    if (profile.LoyaltyPoints < 0) profile.LoyaltyPoints = 0; // Prevent negative balance? Depends on policy.
                }
            }

            // Refund redeemed points (if user used points to pay for this order)
            var redeemed = await _context.LoyaltyPointHistories
                .FirstOrDefaultAsync(h => h.UserId == userId && h.OrderId == orderId && h.Type == PointTransactionType.Redeemed);

            if (redeemed != null)
            {
                // Reverse redemption (add points back)
                var refund = new LoyaltyPointHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrderId = orderId,
                    Points = Math.Abs(redeemed.Points), // Add back
                    Type = PointTransactionType.Adjusted,
                    Description = $"Refund of points used on Order {orderId}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.LoyaltyPointHistories.Add(refund);

                var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
                if (profile != null)
                {
                    profile.LoyaltyPoints += Math.Abs(redeemed.Points);
                }
            }

            await _context.SaveChangesAsync();
        }

        private async Task<LoyaltyPointConfig> GetConfigAsync()
        {
            var config = await _context.LoyaltyPointConfigs.FirstOrDefaultAsync();
            if (config == null)
            {
                // Default config
                return new LoyaltyPointConfig
                {
                    EarnRatePercentage = 10.0m,
                    RedemptionConversionRate = 0.10m, // 10 pts = 1 Tk
                    MinPointsToRedeem = 100,
                    PointsExpiryDays = 365
                };
            }
            return config;
        }
    }
}
