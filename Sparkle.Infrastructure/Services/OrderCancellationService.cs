using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Sparkle.Domain.Orders;
using Sparkle.Infrastructure;

namespace Sparkle.Infrastructure.Services;

public class OrderCancellationService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderCancellationService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromMinutes(5);
    private readonly int _codCancellationMinutes = 30;
    private readonly int _onlinePaymentCancellationMinutes = 60;

    private static readonly HashSet<PaymentMethodType> OnlinePaymentMethods = new()
    {
        PaymentMethodType.BkashPersonal,
        PaymentMethodType.BkashMerchant,
        PaymentMethodType.Nagad,
        PaymentMethodType.Rocket,
        PaymentMethodType.CreditCard,
        PaymentMethodType.DebitCard,
        PaymentMethodType.BankTransfer,
        PaymentMethodType.SparkleWallet,
        PaymentMethodType.Instalment
    };

    public OrderCancellationService(IServiceProvider serviceProvider, ILogger<OrderCancellationService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Order Cancellation Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CancelUnpaidOrdersAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while cancelling unpaid orders");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task CancelUnpaidOrdersAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var notificationService = scope.ServiceProvider.GetService<INotificationService>();

        var now = DateTime.UtcNow;
        var codCutoffTime = now.AddMinutes(-_codCancellationMinutes);
        var onlinePaymentCutoffTime = now.AddMinutes(-_onlinePaymentCancellationMinutes);

        var unpaidOrders = await db.Orders
            .Include(o => o.OrderItems)
            .Where(o => o.PaymentStatus == PaymentStatus.Pending
                     && o.Status == OrderStatus.Pending)
            .ToListAsync(stoppingToken);

        var ordersToCancel = unpaidOrders.Where(o =>
        {
            if (o.PaymentMethod == PaymentMethodType.CashOnDelivery)
            {
                return o.OrderDate <= codCutoffTime;
            }
            else if (OnlinePaymentMethods.Contains(o.PaymentMethod))
            {
                var paymentInitTime = o.PaymentInitiatedAt ?? o.OrderDate;
                return paymentInitTime <= onlinePaymentCutoffTime;
            }
            return false;
        }).ToList();

        if (ordersToCancel.Count > 0)
        {
            _logger.LogInformation("Found {Count} unpaid orders to cancel", ordersToCancel.Count);

            foreach (var order in ordersToCancel)
            {
                try
                {
                    var cancellationMinutes = order.PaymentMethod == PaymentMethodType.CashOnDelivery
                        ? _codCancellationMinutes
                        : _onlinePaymentCancellationMinutes;

                    order.Status = OrderStatus.Cancelled;
                    order.CancelledAt = now;
                    order.CancellationReason = order.PaymentMethod == PaymentMethodType.CashOnDelivery
                        ? $"Payment not completed within {cancellationMinutes} minutes. Order automatically cancelled."
                        : $"Online payment not completed within {cancellationMinutes} minutes. Order automatically cancelled.";

                    foreach (var item in order.OrderItems)
                    {
                        if (item.ProductVariantId.HasValue)
                        {
                            var variant = await db.ProductVariants.FindAsync(new object[] { item.ProductVariantId.Value }, stoppingToken);
                            if (variant != null)
                            {
                                variant.Stock += item.Quantity;
                            }
                        }
                    }

                    _logger.LogInformation("Order {OrderNumber} cancelled due to unpaid {PaymentMethod} payment",
                        order.OrderNumber, order.PaymentMethod);

                    if (notificationService != null)
                    {
                        await notificationService.NotifyUserAsync(
                            order.UserId,
                            "Order Cancelled",
                            $"Your order #{order.OrderNumber} has been automatically cancelled because payment was not completed within {cancellationMinutes} minutes. Please place a new order if you still wish to purchase.",
                            "warning",
                            "/account-info/orders"
                        );
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cancelling order {OrderId}", order.Id);
                }
            }

            await db.SaveChangesAsync(stoppingToken);
        }
    }
}