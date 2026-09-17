using Sparkle.Domain.Orders;
using Sparkle.Domain.Sellers;
using Sparkle.Domain.Wallets;
using Sparkle.Domain.Catalog;

namespace Sparkle.Api.Models;

public class SellerDashboardViewModel
{
    public Seller Seller { get; set; } = default!;
    public SellerWallet? Wallet { get; set; }
    
    // Stats
    public int TotalProducts { get; set; }
    public int TotalSales { get; set; }
    public int NewOrdersCount { get; set; }
    
    // Charts Data (simplified for now)
    public List<decimal> SalesData { get; set; } = new();
    public List<string> SalesLabels { get; set; } = new();

    // Recent Activity
    public List<Order> RecentOrders { get; set; } = new();
    public List<Product> TopProducts { get; set; } = new(); // We might need to map this if Product is in another namespace
}
