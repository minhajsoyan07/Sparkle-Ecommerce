using Sparkle.Domain.Orders;

namespace Sparkle.Api.Areas.Seller.Models;

public class SellerOrderViewModel
{
    public Order Order { get; set; } = null!;
    public bool IsReturningCustomer { get; set; }
    public string MemberSince { get; set; } = string.Empty;
    public decimal SellerTotal { get; set; }
    public int SellerItemCount { get; set; }
    public string CustomerAvatarText { get; set; } = "G";
}
