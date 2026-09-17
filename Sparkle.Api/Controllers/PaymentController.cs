using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure.Services;
using Sparkle.Infrastructure;
using Sparkle.Domain.Orders;
using Sparkle.Api.Models.ViewModels;

namespace Sparkle.Api.Controllers;

public class PaymentController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentService _paymentService;
    private readonly INotificationService _notificationService;

    public PaymentController(ApplicationDbContext db, IPaymentService paymentService, INotificationService notificationService)
    {
        _db = db;
        _paymentService = paymentService;
        _notificationService = notificationService;
    }

    // Gateway Dispatcher
    [Authorize]
    [HttpGet("payment/gateway")]
    public async Task<IActionResult> Gateway(string method, string orderIds)
    {
        var ids = ParseOrderIds(orderIds);
        if (ids.Count == 0)
        {
            TempData["Error"] = "Invalid payment order reference.";
            return Redirect("/");
        }

        var orders = await _db.Orders
            .Include(o => o.User)
            .Include(o => o.OrderItems)
            .Where(o => ids.Contains(o.Id))
            .ToListAsync();

        if (orders.Count != ids.Count)
        {
            TempData["Error"] = "One or more payment orders could not be found.";
            return Redirect("/");
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(ClaimTypes.Name);
        if (!User.IsInRole("Admin") && orders.Any(o => o.UserId != userId))
        {
            return Forbid();
        }

        if (orders.All(o => o.PaymentStatus == PaymentStatus.Paid))
        {
            return RedirectToAction("Confirmation", "Order", new { ids = orderIds });
        }

        if (!TryResolveOnlinePaymentMethod(method, out var paymentMethod))
        {
            TempData["Error"] = "Unsupported payment method.";
            return RedirectToAction("Confirmation", "Order", new { ids = orderIds });
        }

        var canonicalOrderIds = string.Join(",", ids);
        var amount = orders.Sum(o => o.TotalAmount);
        var transactionId = BuildMerchantTransactionId(ids);
        var primaryOrder = orders.OrderBy(o => o.Id).First();

        foreach (var order in orders)
        {
            order.PaymentMethod = paymentMethod;
            order.PaymentStatus = PaymentStatus.Pending;
            order.PaymentTransactionId = transactionId;
        }

        _db.Transactions.Add(new Transaction
        {
            TransactionNumber = transactionId,
            UserId = primaryOrder.UserId,
            TransactionType = "Payment",
            PaymentMethod = paymentMethod,
            Amount = amount,
            Currency = "BDT",
            Status = "Pending",
            GatewayName = "SSLCommerz",
            Notes = $"Orders: {canonicalOrderIds}"
        });

        await _db.SaveChangesAsync();

        try
        {
            var result = await _paymentService.InitiatePaymentAsync(new PaymentInitiationRequest
            {
                TransactionId = transactionId,
                OrderIds = canonicalOrderIds,
                PrimaryOrder = primaryOrder,
                Amount = amount,
                PaymentMethod = paymentMethod,
                SuccessUrl = BuildCallbackUrl("Success", canonicalOrderIds, transactionId),
                FailUrl = BuildCallbackUrl("Fail", canonicalOrderIds, transactionId),
                CancelUrl = BuildCallbackUrl("Cancel", canonicalOrderIds, transactionId),
                IpnUrl = Url.Action(nameof(Ipn), "Payment", null, Request.Scheme)!
            });

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.GatewayPageUrl))
            {
                return Redirect(result.GatewayPageUrl);
            }

            await MarkGatewayTransactionAsync(transactionId, "Failed", result.RawResponse, result.ErrorMessage);
            await _db.SaveChangesAsync();
            TempData["Error"] = result.ErrorMessage ?? "Payment gateway initialization failed.";
            return RedirectToAction("Confirmation", "Order", new { ids = canonicalOrderIds });
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Payment initiation error: {ex.Message}";
            return RedirectToAction("Confirmation", "Order", new { ids = canonicalOrderIds });
        }
    }

    [AllowAnonymous]
    [HttpGet("payment/mock-gateway")]
    public IActionResult MockGateway(string? tran_id = null, string? orderIds = null, string? amount = null, string? success = null, string? fail = null, string? cancel = null, string? method = null)
    {
        ViewBag.TranId = tran_id ?? "SPK-MOCK-ERR";
        ViewBag.OrderIds = orderIds ?? "0";
        ViewBag.Amount = amount ?? "0.00";
        ViewBag.SuccessUrl = success ?? "/";
        ViewBag.FailUrl = fail ?? "/";
        ViewBag.CancelUrl = cancel ?? "/";
        ViewBag.Method = method;
        return View();
    }

    [AllowAnonymous]
    [HttpPost("payment/mock-process")]
    [IgnoreAntiforgeryToken]
    public IActionResult MockProcess(string actionType, string tran_id, string orderIds, string success, string fail, string cancel)
    {
        // Fallbacks
        success = string.IsNullOrEmpty(success) ? "/" : success;
        fail = string.IsNullOrEmpty(fail) ? "/" : fail;
        cancel = string.IsNullOrEmpty(cancel) ? "/" : cancel;

        if (actionType == "success")
        {
            var valId = "MOCK-VAL-" + (string.IsNullOrEmpty(tran_id) ? Guid.NewGuid().ToString("N").ToUpper() : tran_id);
            var separator = success.Contains("?") ? "&" : "?";
            
            // Avoid duplicate parameters if success URL already has them
            var redirectUrl = success;
            if (!redirectUrl.Contains("val_id=")) redirectUrl += $"{separator}val_id={valId}";
            separator = redirectUrl.Contains("?") ? "&" : "?";
            if (!redirectUrl.Contains("status=")) redirectUrl += $"{separator}status=VALID";
            
            return Redirect(redirectUrl);
        }
        else if (actionType == "fail")
        {
            var separator = fail.Contains("?") ? "&" : "?";
            var redirectUrl = fail;
            if (!redirectUrl.Contains("status=")) redirectUrl += $"{separator}status=FAILED";
            return Redirect(redirectUrl);
        }
        else
        {
            var separator = cancel.Contains("?") ? "&" : "?";
            var redirectUrl = cancel;
            if (!redirectUrl.Contains("status=")) redirectUrl += $"{separator}status=CANCELLED";
            return Redirect(redirectUrl);
        }
    }

    [HttpPost("payment/success")]
    [HttpGet("payment/success")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Success(string? orderIds, string? tran_id)
    {
        var callback = await ReadGatewayCallbackAsync(orderIds, tran_id);
        var result = await FinalizeGatewayPaymentAsync(callback);

        if (result.Success)
        {
            var model = new PaymentStatusViewModel
            {
                Type = "success",
                Status = "Payment Successful",
                Message = "Your payment has been processed successfully. Your order is now confirmed.",
                TransactionId = tran_id,
                OrderIds = orderIds,
                Amount = result.Amount
            };
            return View("Status", model);
        }
        else
        {
            var model = new PaymentStatusViewModel
            {
                Type = "failed",
                Status = "Payment Failed",
                Message = result.Message ?? "We could not verify your payment.",
                TransactionId = tran_id,
                OrderIds = orderIds
            };
            return View("Status", model);
        }
    }

    [HttpPost("payment/ipn")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Ipn()
    {
        var callback = await ReadGatewayCallbackAsync(null, null);
        var result = await FinalizeGatewayPaymentAsync(callback);
        return result.Success ? Ok("VALID") : BadRequest(result.Message);
    }

    [HttpPost("payment/fail")]
    [HttpGet("payment/fail")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Fail(string? orderIds, string? tran_id)
    {
        var callback = await ReadGatewayCallbackAsync(orderIds, tran_id);
        await MarkOrdersPaymentFailedAsync(callback.OrderIds, callback.TransactionId, "Failed", callback.RawPayload);
        
        var model = new PaymentStatusViewModel
        {
            Type = "failed",
            Status = "Payment Failed",
            Message = "Your payment attempt was unsuccessful. Please check your card/wallet details and try again.",
            TransactionId = tran_id,
            OrderIds = orderIds
        };
        return View("Status", model);
    }

    [HttpPost("payment/cancel")]
    [HttpGet("payment/cancel")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Cancel(string? orderIds, string? tran_id)
    {
        var callback = await ReadGatewayCallbackAsync(orderIds, tran_id);
        // Mark both the transaction AND the orders as Failed (no Cancelled status exists)
        // so orders no longer show as PENDING in the dashboard
        await MarkOrdersPaymentFailedAsync(callback.OrderIds, callback.TransactionId, "Cancelled", callback.RawPayload);
        
        var model = new PaymentStatusViewModel
        {
            Type = "cancelled",
            Status = "Payment Cancelled",
            Message = "You have cancelled the payment process. Your order remains unconfirmed.",
            TransactionId = tran_id,
            OrderIds = orderIds
        };
        return View("Status", model);
    }

    private static bool TryResolveOnlinePaymentMethod(string? method, out PaymentMethodType paymentMethod)
    {
        switch (method?.Trim().ToLowerInvariant())
        {
            case "bkash":
            case "bkashpersonal":
                paymentMethod = PaymentMethodType.BkashPersonal;
                return true;
            case "nagad":
                paymentMethod = PaymentMethodType.Nagad;
                return true;
            case "rocket":
                paymentMethod = PaymentMethodType.Rocket;
                return true;
            case "card":
            case "creditcard":
                paymentMethod = PaymentMethodType.CreditCard;
                return true;
            case "debit-card":
            case "debitcard":
                paymentMethod = PaymentMethodType.DebitCard;
                return true;
            case "instalment":
                paymentMethod = PaymentMethodType.Instalment;
                return true;
            default:
                paymentMethod = default;
                return false;
        }
    }

    private static List<int> ParseOrderIds(string? orderIds)
    {
        if (string.IsNullOrWhiteSpace(orderIds))
        {
            return new List<int>();
        }

        return orderIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0)
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();
    }

    private string BuildCallbackUrl(string action, string orderIds, string transactionId)
    {
        return Url.Action(action, "Payment", new { orderIds, tran_id = transactionId }, Request.Scheme)!;
    }

    private static string BuildMerchantTransactionId(IEnumerable<int> orderIds)
    {
        var suffix = Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        return $"SPK-{DateTime.UtcNow:yyyyMMddHHmmss}-{orderIds.First()}-{suffix}";
    }

    private async Task<GatewayCallback> ReadGatewayCallbackAsync(string? orderIds, string? transactionId)
    {
        IFormCollection? form = null;
        if (Request.HasFormContentType)
        {
            form = await Request.ReadFormAsync();
        }

        var resolvedOrderIds = FirstValue(orderIds, GetValue(form, "value_a"), GetValue(form, "orderIds"), GetValue(form, "order_ids"));
        var resolvedTransactionId = FirstValue(transactionId, GetValue(form, "tran_id"), GetValue(form, "tranId"), Request.Query["tran_id"].FirstOrDefault());
        var validationId = FirstValue(GetValue(form, "val_id"), Request.Query["val_id"].FirstOrDefault());

        var payload = form != null && form.Count > 0
            ? JsonSerializer.Serialize(form.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()))
            : JsonSerializer.Serialize(Request.Query.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToString()));

        if (string.IsNullOrWhiteSpace(resolvedOrderIds) && !string.IsNullOrWhiteSpace(resolvedTransactionId))
        {
            var matchingOrderIds = await _db.Orders
                .Where(o => o.PaymentTransactionId == resolvedTransactionId)
                .OrderBy(o => o.Id)
                .Select(o => o.Id)
                .ToListAsync();

            resolvedOrderIds = string.Join(",", matchingOrderIds);
        }

        return new GatewayCallback
        {
            OrderIds = resolvedOrderIds ?? "",
            TransactionId = resolvedTransactionId ?? "",
            ValidationId = validationId ?? "",
            RawPayload = payload
        };
    }

    private async Task<PaymentCompletionResult> FinalizeGatewayPaymentAsync(GatewayCallback callback)
    {
        if (string.IsNullOrWhiteSpace(callback.ValidationId))
        {
            await MarkGatewayTransactionAsync(callback.TransactionId, "Failed", callback.RawPayload, "Missing validation id.");
            await _db.SaveChangesAsync();
            return new PaymentCompletionResult(false, callback.OrderIds, "Payment validation data was missing.", 0);
        }

        var validation = await _paymentService.ValidatePaymentAsync(callback.ValidationId);
        var rawResponse = validation.RawResponse ?? callback.RawPayload;
        var orderIds = ParseOrderIds(callback.OrderIds);

        if (orderIds.Count == 0 && !string.IsNullOrWhiteSpace(validation.TransactionId))
        {
            orderIds = await _db.Orders
                .Where(o => o.PaymentTransactionId == validation.TransactionId)
                .OrderBy(o => o.Id)
                .Select(o => o.Id)
                .ToListAsync();
            callback = callback with { OrderIds = string.Join(",", orderIds) };
        }

        var orders = await _db.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync();
        if (orders.Count == 0)
        {
            await MarkGatewayTransactionAsync(validation.TransactionId, "Failed", rawResponse, "No matching order found.");
            await _db.SaveChangesAsync();
            return new PaymentCompletionResult(false, callback.OrderIds, "No matching order found for this payment.", 0);
        }

        var expectedTransactionId = orders.First().PaymentTransactionId ?? callback.TransactionId;
        if (!string.Equals(validation.TransactionId, expectedTransactionId, StringComparison.OrdinalIgnoreCase))
        {
            await MarkGatewayTransactionAsync(expectedTransactionId, "Failed", rawResponse, "Transaction id mismatch.");
            await _db.SaveChangesAsync();
            return new PaymentCompletionResult(false, callback.OrderIds, "Payment transaction id did not match the order.", 0);
        }

        var expectedAmount = orders.Sum(o => o.TotalAmount);
        if (validation.Currency != "BDT" || Math.Abs(validation.Amount - expectedAmount) > 0.01m)
        {
            await MarkGatewayTransactionAsync(expectedTransactionId, "Failed", rawResponse, "Amount or currency mismatch.");
            await _db.SaveChangesAsync();
            return new PaymentCompletionResult(false, callback.OrderIds, "Payment amount or currency did not match the order.", expectedAmount);
        }

        if (!validation.IsValid)
        {
            await MarkOrdersPaymentFailedAsync(callback.OrderIds, expectedTransactionId, validation.Status, rawResponse);
            return new PaymentCompletionResult(false, callback.OrderIds, $"Payment validation failed: {validation.Status}", expectedAmount);
        }

        if (validation.RiskLevel == 1)
        {
            await MarkGatewayTransactionAsync(expectedTransactionId, "Pending", rawResponse, validation.RiskTitle ?? "Risk payment requires review.");
            await _db.SaveChangesAsync();
            return new PaymentCompletionResult(false, callback.OrderIds, "Payment is pending risk review.", expectedAmount);
        }

        foreach (var order in orders)
        {
            order.PaymentStatus = PaymentStatus.Paid;
            order.PaymentTransactionId = validation.TransactionId;
            order.PaidAt = DateTime.UtcNow;
        }

        await MarkGatewayTransactionAsync(validation.TransactionId, "Success", rawResponse, null, validation.BankTransactionId);
        await _db.SaveChangesAsync();

        return new PaymentCompletionResult(true, string.Join(",", orderIds), "Payment completed successfully.", expectedAmount);
    }

    private async Task MarkOrdersPaymentFailedAsync(string? orderIds, string? transactionId, string status, string? gatewayResponse)
    {
        var ids = ParseOrderIds(orderIds);
        var orders = await _db.Orders
            .Where(o => ids.Contains(o.Id) || (!string.IsNullOrEmpty(transactionId) && o.PaymentTransactionId == transactionId))
            .ToListAsync();

        foreach (var order in orders.Where(o => o.PaymentStatus != PaymentStatus.Paid))
        {
            order.PaymentStatus = PaymentStatus.Failed;
        }

        await MarkGatewayTransactionAsync(transactionId, status, gatewayResponse, null);
        await _db.SaveChangesAsync();
    }

    private async Task MarkGatewayTransactionAsync(string? transactionId, string status, string? gatewayResponse, string? failureReason, string? bankTransactionId = null)
    {
        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return;
        }

        var tx = await _db.Transactions.FirstOrDefaultAsync(t => t.TransactionNumber == transactionId);
        if (tx == null)
        {
            return;
        }

        tx.Status = status;
        tx.GatewayResponse = gatewayResponse;
        tx.FailureReason = failureReason;
        tx.GatewayTransactionId = bankTransactionId ?? tx.GatewayTransactionId;
        tx.CompletedAt = status.Equals("Success", StringComparison.OrdinalIgnoreCase) ? DateTime.UtcNow : tx.CompletedAt;
    }

    private IActionResult RedirectToConfirmationOrHome(string? orderIds)
    {
        return string.IsNullOrWhiteSpace(orderIds)
            ? Redirect("/")
            : RedirectToAction("Confirmation", "Order", new { ids = orderIds });
    }

    private static string? GetValue(IFormCollection? form, string key)
    {
        return form != null && form.TryGetValue(key, out var value) ? value.FirstOrDefault() : null;
    }

    private static string? FirstValue(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private sealed record GatewayCallback
    {
        public string OrderIds { get; init; } = "";
        public string TransactionId { get; init; } = "";
        public string ValidationId { get; init; } = "";
        public string RawPayload { get; init; } = "";
    }

    private sealed record PaymentCompletionResult(bool Success, string OrderIds, string Message, decimal Amount = 0);
}
