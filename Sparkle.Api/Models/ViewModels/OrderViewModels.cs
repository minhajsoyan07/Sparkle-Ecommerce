using Sparkle.Domain.Orders;

namespace Sparkle.Api.Models.ViewModels;

public record PagedUserOrdersViewModel(
    IReadOnlyCollection<Order> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}
