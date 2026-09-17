using Sparkle.Domain.Common;

namespace Sparkle.Domain.Sellers;

public class SellerPerformanceScore : BaseEntity
{
    public int SellerId { get; set; }
    public Seller Seller { get; set; } = null!;

    // Overall Score (0-100)
    public decimal OverallScore { get; set; }

    // Component Scores (0-100 each)
    public decimal SalesScore { get; set; }
    public decimal QualityScore { get; set; }
    public decimal ResponseScore { get; set; }
    public decimal DeliveryScore { get; set; }

    // Metrics
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public int AverageResponseTimeMinutes { get; set; }
    public decimal OnTimeDeliveryRate { get; set; }

    // Metadata
    public string Period { get; set; } = "Last 3 Months";
    public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
}
