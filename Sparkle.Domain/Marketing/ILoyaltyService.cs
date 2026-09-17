using System;
using System.Threading.Tasks;

namespace Sparkle.Domain.Marketing
{
    public interface ILoyaltyService
    {
        Task<int> GetUserPointBalanceAsync(string userId);
        Task EarnPointsAsync(string userId, Guid orderId, decimal orderAmount);
        Task<bool> RedeemPointsAsync(string userId, Guid orderId, int pointsToRedeem);
        Task<decimal> CalculateDiscountValueAsync(int points);
        Task RefundPointsAsync(string userId, Guid orderId);
    }
}
