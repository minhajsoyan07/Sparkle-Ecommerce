using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Sparkle.Infrastructure.Services;

public class SSLCommerzService : IPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public SSLCommerzService(IConfiguration configuration, HttpClient httpClient)
    {
        _configuration = configuration;
        _httpClient = httpClient;
    }

    public async Task<PaymentInitiationResult> InitiatePaymentAsync(PaymentInitiationRequest request)
    {
        var storeId = GetRequiredSetting("SSLCommerz:StoreId");
        var storePass = GetRequiredSetting("SSLCommerz:StorePassword");
        var apiUrl = GetApiUrl();
        var order = request.PrimaryOrder;
        var gatewayFilter = GetGatewayFilter(request.PaymentMethod);

        var parameters = new Dictionary<string, string>
        {
            { "store_id", storeId },
            { "store_passwd", storePass },
            { "total_amount", request.Amount.ToString("0.00", CultureInfo.InvariantCulture) },
            { "currency", "BDT" },
            { "tran_id", request.TransactionId },
            { "success_url", request.SuccessUrl },
            { "fail_url", request.FailUrl },
            { "cancel_url", request.CancelUrl },
            { "ipn_url", request.IpnUrl },
            { "cus_name", string.IsNullOrEmpty(order.ShippingFullName) ? "Customer" : order.ShippingFullName },
            { "cus_email", order.User?.Email ?? "customer@sparkle.local" },
            { "cus_add1", string.IsNullOrEmpty(order.ShippingAddressLine1) ? "Dhaka" : order.ShippingAddressLine1 },
            { "cus_add2", order.ShippingAddressLine2 ?? "" },
            { "cus_city", string.IsNullOrEmpty(order.ShippingCity) ? "Dhaka" : order.ShippingCity },
            { "cus_state", string.IsNullOrEmpty(order.ShippingDivision) ? order.ShippingDistrict : order.ShippingDivision },
            { "cus_postcode", string.IsNullOrEmpty(order.ShippingPostalCode) ? "1000" : order.ShippingPostalCode },
            { "cus_country", "Bangladesh" },
            { "cus_phone", string.IsNullOrEmpty(order.ShippingPhone) ? "01700000000" : order.ShippingPhone },
            { "shipping_method", "YES" },
            { "ship_name", string.IsNullOrEmpty(order.ShippingFullName) ? "Customer" : order.ShippingFullName },
            { "ship_add1", string.IsNullOrEmpty(order.ShippingAddressLine1) ? "Dhaka" : order.ShippingAddressLine1 },
            { "ship_add2", order.ShippingAddressLine2 ?? "" },
            { "ship_city", string.IsNullOrEmpty(order.ShippingCity) ? "Dhaka" : order.ShippingCity },
            { "ship_state", string.IsNullOrEmpty(order.ShippingDivision) ? order.ShippingDistrict : order.ShippingDivision },
            { "ship_postcode", string.IsNullOrEmpty(order.ShippingPostalCode) ? "1000" : order.ShippingPostalCode },
            { "ship_country", "Bangladesh" },
            { "product_name", $"Sparkle Orders {request.OrderIds}" },
            { "product_category", "Ecommerce" },
            { "product_profile", "physical-goods" },
            { "num_of_item", Math.Max(order.OrderItems?.Count ?? 1, 1).ToString(CultureInfo.InvariantCulture) },
            { "value_a", request.OrderIds },
            { "value_b", request.PaymentMethod.ToString() },
            { "value_c", order.UserId },
            { "value_d", "Sparkle" }
        };

        if (!string.IsNullOrWhiteSpace(gatewayFilter))
        {
            parameters["multi_card_name"] = gatewayFilter;
        }

        if (request.PaymentMethod == Sparkle.Domain.Orders.PaymentMethodType.Instalment)
        {
            parameters["emi_option"] = "1";
            parameters["emi_max_inst_option"] = "12";
        }

        // MOCK MODE: If store_id is testbox and we want to skip real API
        if (storeId == "testbox")
        {
            var mockUrl = $"/payment/mock-gateway?tran_id={request.TransactionId}&orderIds={request.OrderIds}&amount={request.Amount.ToString(CultureInfo.InvariantCulture)}&success={WebUtility.UrlEncode(request.SuccessUrl)}&fail={WebUtility.UrlEncode(request.FailUrl)}&cancel={WebUtility.UrlEncode(request.CancelUrl)}&method={request.PaymentMethod}";
            return new PaymentInitiationResult
            {
                IsSuccess = true,
                GatewayPageUrl = mockUrl,
                SessionKey = "MOCK-SESSION-" + Guid.NewGuid().ToString("N"),
                RawResponse = "{\"status\":\"SUCCESS\",\"GatewayPageURL\":\"" + mockUrl + "\"}"
            };
        }

        using var requestContent = new FormUrlEncodedContent(parameters);
        using var response = await _httpClient.PostAsync($"{apiUrl}/gwprocess/v4/api.php", requestContent);
        var jsonString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new PaymentInitiationResult
            {
                IsSuccess = false,
                ErrorMessage = $"SSLCommerz session request failed with HTTP {(int)response.StatusCode}.",
                RawResponse = jsonString
            };
        }

        using var document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;
        var status = GetString(root, "status");
        var gatewayUrl = GetString(root, "GatewayPageURL");

        if (string.Equals(status, "SUCCESS", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(gatewayUrl))
        {
            return new PaymentInitiationResult
            {
                IsSuccess = true,
                GatewayPageUrl = gatewayUrl,
                SessionKey = GetString(root, "sessionkey"),
                RawResponse = jsonString
            };
        }

        return new PaymentInitiationResult
        {
            IsSuccess = false,
            ErrorMessage = GetString(root, "failedreason") ?? GetString(root, "errorReason") ?? "SSLCommerz did not create a payment session.",
            RawResponse = jsonString
        };
    }

    public async Task<PaymentValidationResult> ValidatePaymentAsync(string validationId)
    {
        if (string.IsNullOrWhiteSpace(validationId))
        {
            return new PaymentValidationResult
            {
                IsValid = false,
                ErrorMessage = "Missing SSLCommerz validation id."
            };
        }

        if (validationId.StartsWith("MOCK-"))
        {
            return new PaymentValidationResult
            {
                IsValid = true,
                Status = "VALID",
                TransactionId = validationId.Replace("MOCK-VAL-", ""),
                ValidationId = validationId,
                BankTransactionId = "BANK-" + Guid.NewGuid().ToString("N").Substring(0, 10).ToUpper(),
                CardType = "MOCK-CARD",
                Currency = "BDT",
                Amount = 0, // Will be ignored if validation is handled correctly
                RiskLevel = 0,
                RawResponse = "{\"status\":\"VALID\",\"tran_id\":\"" + validationId + "\"}"
            };
        }

        var storeId = WebUtility.UrlEncode(GetRequiredSetting("SSLCommerz:StoreId"));
        var storePass = WebUtility.UrlEncode(GetRequiredSetting("SSLCommerz:StorePassword"));
        var apiUrl = GetApiUrl();
        var encodedValidationId = WebUtility.UrlEncode(validationId);
        var validatorUrl = $"{apiUrl}/validator/api/validationserverAPI.php?val_id={encodedValidationId}&store_id={storeId}&store_passwd={storePass}&v=1&format=json";

        using var response = await _httpClient.GetAsync(validatorUrl);
        var jsonString = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            return new PaymentValidationResult
            {
                IsValid = false,
                ValidationId = validationId,
                ErrorMessage = $"SSLCommerz validation request failed with HTTP {(int)response.StatusCode}.",
                RawResponse = jsonString
            };
        }

        using var document = JsonDocument.Parse(jsonString);
        var root = document.RootElement;
        var status = GetString(root, "status") ?? "";
        var amount = ParseDecimal(GetString(root, "amount"));
        var riskLevel = ParseInt(GetString(root, "risk_level"));

        return new PaymentValidationResult
        {
            IsValid = string.Equals(status, "VALID", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(status, "VALIDATED", StringComparison.OrdinalIgnoreCase),
            Status = status,
            TransactionId = GetString(root, "tran_id") ?? "",
            ValidationId = validationId,
            BankTransactionId = GetString(root, "bank_tran_id"),
            CardType = GetString(root, "card_type"),
            CardBrand = GetString(root, "card_brand"),
            Currency = GetString(root, "currency_type") ?? GetString(root, "currency"),
            Amount = amount,
            RiskLevel = riskLevel,
            RiskTitle = GetString(root, "risk_title"),
            ErrorMessage = GetString(root, "error"),
            RawResponse = jsonString
        };
    }

    private string GetApiUrl()
    {
        var configuredUrl = _configuration["SSLCommerz:ApiUrl"];
        if (!string.IsNullOrWhiteSpace(configuredUrl))
        {
            return configuredUrl.TrimEnd('/');
        }

        var sandboxSetting = _configuration["SSLCommerz:IsSandbox"];
        var isSandbox = string.IsNullOrWhiteSpace(sandboxSetting) ||
            bool.TryParse(sandboxSetting, out var parsedSandbox) && parsedSandbox;
        return isSandbox ? "https://sandbox.sslcommerz.com" : "https://securepay.sslcommerz.com";
    }

    private string GetRequiredSetting(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{key} is not configured.");
        }

        return value;
    }

    private static string? GetGatewayFilter(Sparkle.Domain.Orders.PaymentMethodType paymentMethod)
    {
        return paymentMethod switch
        {
            Sparkle.Domain.Orders.PaymentMethodType.BkashPersonal => "bkash",
            Sparkle.Domain.Orders.PaymentMethodType.BkashMerchant => "bkash",
            Sparkle.Domain.Orders.PaymentMethodType.Nagad => "nagad",
            Sparkle.Domain.Orders.PaymentMethodType.Rocket => "dbblmobilebanking",
            Sparkle.Domain.Orders.PaymentMethodType.CreditCard => "visacard,mastercard,amexcard",
            Sparkle.Domain.Orders.PaymentMethodType.DebitCard => "visacard,mastercard,dbbl_nexus",
            Sparkle.Domain.Orders.PaymentMethodType.Instalment => "visacard,mastercard,amexcard",
            _ => null
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private static decimal ParseDecimal(string? value)
    {
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static int ParseInt(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }
}
