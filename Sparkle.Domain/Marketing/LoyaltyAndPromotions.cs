using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sparkle.Domain.Marketing
{
    public enum PointTransactionType
    {
        Earned,
        Redeemed,
        Expired,
        Adjusted
    }

    public class LoyaltyPointConfig
    {
        [Key]
        public int Id { get; set; }

        public decimal EarnRatePercentage { get; set; } = 10.0m; // 10% of order value
        public decimal RedemptionConversionRate { get; set; } = 0.10m; // 1 Point = 0.10 Tk (10 points = 1 Tk)
        public int MinPointsToRedeem { get; set; } = 100;
        public int PointsExpiryDays { get; set; } = 365;
    }

    public class LoyaltyPointHistory
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        public Guid? OrderId { get; set; } // Nullable for manual adjustments

        public int Points { get; set; } // Positive for earn, Negative for redeem

        [Required]
        public PointTransactionType Type { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ExpiryDate { get; set; }
    }
}
