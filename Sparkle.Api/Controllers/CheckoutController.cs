using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sparkle.Infrastructure.Services;
using Sparkle.Api.Services;
using Sparkle.Domain.Orders;
using Sparkle.Domain.Catalog;
using Sparkle.Domain.Sellers;
using Sparkle.Infrastructure;

namespace Sparkle.Api.Controllers;

[Authorize]
[Route("checkout")]
public class CheckoutController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ICommissionService _commissionService;
    private readonly INotificationService _notificationService;

    public CheckoutController(ApplicationDbContext db, ICommissionService commissionService, INotificationService notificationService)
    {
        _db = db;
        _commissionService = commissionService;
        _notificationService = notificationService;
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ??
        User.FindFirstValue(ClaimTypes.Name) ??
        throw new InvalidOperationException("User id not found in token");

    [HttpGet("")]
    public IActionResult Index()
    {
        return RedirectToAction(nameof(Address));
    }

    // Step 1: Shipping Address
    [HttpGet("address")]
    public async Task<IActionResult> Address()
    {
        if (User.IsInRole("Seller")) return Redirect("/seller/dashboard");

        var userId = GetUserId();
        var cart = await _db.Carts
            .Include(c => c.Items)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.Items.Any())
        {
            return Redirect("/cart");
        }

        var addresses = await _db.Addresses.Where(a => a.UserId == userId).OrderByDescending(a => a.IsDefault).ToListAsync<Sparkle.Domain.Orders.Address>();
        var vm = new AddressStepViewModel
        {
            Cart = cart,
            SavedAddresses = addresses,
            NewAddress = new CheckoutAddressModel()
        };
        return View(vm);
    }

    [HttpPost("address")]
    public async Task<IActionResult> Address(CheckoutAddressModel model, int? savedAddressId, [FromForm] int? editAddressId)
    {
        var userId = GetUserId();
        
        int addressId;
        if (savedAddressId.HasValue && !editAddressId.HasValue)
        {
            addressId = savedAddressId.Value;
        }
        else
        {
            if (!ModelState.IsValid)
            {
                return RedirectToAction(nameof(Address));
            }
            
            if (editAddressId.HasValue)
            {
                // Update Existing Address
                var existingAddr = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == editAddressId.Value && a.UserId == userId);
                
                if (existingAddr != null)
                {
                    if (model.IsDefault)
                    {
                        var otherDefault = await _db.Addresses.FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault && a.Id != existingAddr.Id);
                        if (otherDefault != null)
                        {
                            otherDefault.IsDefault = false;
                            _db.Addresses.Update(otherDefault);
                        }
                    }

                    existingAddr.FullName = model.FullName;
                    existingAddr.Phone = model.Phone;
                    existingAddr.Line1 = model.Line1;
                    existingAddr.Line2 = model.Line2;
                    existingAddr.City = model.City;
                    existingAddr.State = model.State;
                    existingAddr.Area = model.Area;
                    existingAddr.PostalCode = model.PostalCode;
                    existingAddr.Country = model.Country;
                    existingAddr.IsDefault = model.IsDefault;
                    
                    _db.Addresses.Update(existingAddr);
                    await _db.SaveChangesAsync();
                    addressId = existingAddr.Id;
                }
                else
                {
                    // Fallback to create if not found (Should ideally show error, but creating is safe fallback)
                    var addr = new Sparkle.Domain.Orders.Address { 
                        UserId = userId, 
                        FullName = model.FullName, 
                        Phone = model.Phone, 
                        Line1 = model.Line1, 
                        Line2 = model.Line2, 
                        City = model.City, 
                        State = model.State, 
                        Area = model.Area, 
                        PostalCode = model.PostalCode, 
                        Country = model.Country,
                        IsDefault = model.IsDefault
                    };
                    _db.Addresses.Add(addr);
                    await _db.SaveChangesAsync();
                    addressId = addr.Id;
                }
            }
            else
            {
                // Create New
                if (model.IsDefault)
                {
                    var otherDefault = await _db.Addresses.FirstOrDefaultAsync(a => a.UserId == userId && a.IsDefault);
                    if (otherDefault != null)
                    {
                        otherDefault.IsDefault = false;
                        _db.Addresses.Update(otherDefault);
                    }
                }

                var addr = new Sparkle.Domain.Orders.Address
                {
                    UserId = userId,
                    FullName = model.FullName,
                    Phone = model.Phone,
                    Line1 = model.Line1,
                    Line2 = model.Line2,
                    City = model.City,
                    State = model.State,
                    Area = model.Area,
                    PostalCode = model.PostalCode,
                    Country = model.Country,
                    IsDefault = model.IsDefault
                };
                _db.Addresses.Add(addr);
                await _db.SaveChangesAsync();
                addressId = addr.Id;
            }
        }

        HttpContext.Session.SetInt32("CheckoutAddressId", addressId);
        if (!string.IsNullOrEmpty(model.Area))
        {
            HttpContext.Session.SetString("CheckoutArea", model.Area);
        }
        
        // Skip Delivery Step - Default to Standard/60
        HttpContext.Session.SetString("DeliveryMethod", "Standard Delivery");
        HttpContext.Session.SetString("ShippingFee", "60");

        return RedirectToAction(nameof(Payment));
    }

    // Step 3: Payment Method
    [HttpGet("payment")]
    public async Task<IActionResult> Payment()
    {
        if (User.IsInRole("Seller")) return Redirect("/seller/dashboard");

        var userId = GetUserId();
        var cart = await _db.Carts
            .Include(c => c.Items)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(v => v.Product)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.Items.Any())
        {
            return Redirect("/cart");
        }

        var addressId = HttpContext.Session.GetInt32("CheckoutAddressId");
        var deliveryMethod = HttpContext.Session.GetString("DeliveryMethod");
        if (addressId == null || string.IsNullOrEmpty(deliveryMethod))
        {
            return RedirectToAction(nameof(Address));
        }

        var currentPaymentMethod = HttpContext.Session.GetString("PaymentMethod");
        var shippingFeeStr = HttpContext.Session.GetString("ShippingFee");
        decimal.TryParse(shippingFeeStr, out decimal shippingFee);

        var vm = new PaymentStepViewModel
        {
            Cart = cart,
            ShippingFee = shippingFee
        };
        ViewBag.SelectedPaymentMethod = currentPaymentMethod;
        return View(vm);
    }

    [HttpPost("payment")]
    public async Task<IActionResult> Payment(string paymentMethod)
    {
        HttpContext.Session.SetString("PaymentMethod", paymentMethod);

        if (User.IsInRole("Seller")) return Redirect("/seller/dashboard");

        var userId = GetUserId();
        var cart = await _db.Carts
            .Include(c => c.Items)
                .ThenInclude(i => i.ProductVariant)
                    .ThenInclude(v => v.Product)
                        .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (cart == null || !cart.Items.Any())
        {
            return Redirect("/cart");
        }

        // STEP 1: Re-validate Stock
        foreach (var item in cart.Items)
        {
            if (item.ProductVariant == null || item.ProductVariant.Stock < item.Quantity)
            {
                TempData["Error"] = "One of the items in your cart is out of stock or no longer available.";
                return RedirectToAction(nameof(Payment));
            }
        }

        var addressId = HttpContext.Session.GetInt32("CheckoutAddressId");
        var shippingArea = HttpContext.Session.GetString("CheckoutArea") ?? "";
        var shippingFeeStr = HttpContext.Session.GetString("ShippingFee");
        if (addressId == null)
        {
            TempData["Error"] = "Shipping address not found in session.";
            return RedirectToAction(nameof(Address));
        }

        decimal totalShippingFee = 0;
        if (!string.IsNullOrEmpty(shippingFeeStr))
        {
            decimal.TryParse(shippingFeeStr, System.Globalization.CultureInfo.InvariantCulture, out totalShippingFee);
        }
        decimal totalCartSubtotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
        if (totalCartSubtotal == 0) totalCartSubtotal = 1;

        var address = await _db.Addresses.FindAsync(addressId.Value);
        if (address == null)
        {
            return RedirectToAction(nameof(Address));
        }

        if (!TryResolvePaymentMethod(paymentMethod, out var selectedPaymentMethod))
        {
            TempData["Error"] = "Please select a valid payment method.";
            return RedirectToAction(nameof(Payment));
        }

        var sellerGroups = cart.Items.GroupBy(i => i.ProductVariant?.Product?.SellerId);

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                var createdOrderIds = new List<int>();
                var parentOrderNumber = $"ORD-GRP-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 6).ToUpper()}";
                foreach (var group in sellerGroups)
                {
                    var sellerItems = group.ToList();
                    var currentSellerId = group.Key;
                    
                    var groupSubtotal = sellerItems.Sum(i => i.UnitPrice * i.Quantity);
                    var groupShippingFee = Math.Round((groupSubtotal / totalCartSubtotal) * totalShippingFee, 2);

                    var isOnlinePayment = selectedPaymentMethod != PaymentMethodType.CashOnDelivery;
                    var order = new Order
                    {
                        OrderNumber = $"ORD-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}",
                        ParentOrderNumber = parentOrderNumber,
                        UserId = userId,
                        SellerId = currentSellerId,
                        Status = OrderStatus.Pending,
                        PaymentStatus = PaymentStatus.Pending,
                        PaymentMethod = selectedPaymentMethod,
                        PaymentInitiatedAt = isOnlinePayment ? DateTime.UtcNow : null,
                        DeliveryMode = DeliveryMode.CourierAssisted,
                        SubTotal = groupSubtotal,
                        ShippingCost = groupShippingFee,
                        TotalAmount = groupSubtotal + groupShippingFee,
                        OrderDate = DateTime.UtcNow,
                        ShippingFullName = address.FullName,
                        ShippingPhone = address.Phone,
                        ShippingAddressLine1 = address.Line1,
                        ShippingAddressLine2 = address.Line2 ?? "",
                        ShippingCity = address.City,
                        ShippingDistrict = address.City,
                        ShippingDivision = address.State,
                        ShippingPostalCode = address.PostalCode,
                        ShippingCountry = address.Country ?? "Bangladesh",
                        ShippingArea = shippingArea,
                        OrderItems = sellerItems.Select(i => new OrderItem
                        {
                            ProductId = i.ProductVariant.ProductId,
                            ProductVariantId = i.ProductVariantId,
                            ProductName = i.ProductVariant.Product.Title,
                            VariantName = $"{i.ProductVariant.Color} {i.ProductVariant.Size}".Trim(),
                            ProductImage = i.ProductVariant.Product.Images.OrderBy(img => img.SortOrder).FirstOrDefault()?.Url,
                            Quantity = i.Quantity,
                            UnitPrice = i.UnitPrice,
                            TotalPrice = i.UnitPrice * i.Quantity,
                            SellerId = currentSellerId,
                            ItemStatus = OrderStatus.Pending
                        }).ToList()
                    };

                    _db.Orders.Add(order);
                    
                    foreach (var item in sellerItems)
                    {
                        var variant = await _db.ProductVariants.FindAsync(item.ProductVariantId);
                        if (variant != null)
                        {
                            variant.Stock = Math.Max(0, variant.Stock - item.Quantity);
                        }
                    }

                    await _db.SaveChangesAsync();
                    createdOrderIds.Add(order.Id);
                    
                    await _commissionService.ProcessOrderCommissionAsync(order.Id);

                    if (currentSellerId.HasValue)
                    {
                        await _notificationService.NotifySellerAsync(currentSellerId.Value, 
                            "New Order Received", 
                            $"You have a new order (#{order.OrderNumber}) awaiting approval.", 
                            "info", 
                            $"/seller/orders/details/{order.Id}");
                    }
                }

                _db.CartItems.RemoveRange(cart.Items);
                _db.Carts.Remove(cart);
                await _db.SaveChangesAsync();

                await transaction.CommitAsync();

                // Notification Logic: Only show "Order Placed Successfully" for COD.
                // For online payment, we wait until payment success.
                if (selectedPaymentMethod == PaymentMethodType.CashOnDelivery)
                {
                    await _notificationService.NotifyUserAsync(userId,
                        "Order Placed Successfully",
                        $"Your order has been placed successfully via Cash on Delivery. Order ID: {string.Join(", ", createdOrderIds)}",
                        "success",
                        "/account-info/orders");
                }

                HttpContext.Session.Remove("CheckoutAddressId");
                HttpContext.Session.Remove("CheckoutArea");
                HttpContext.Session.Remove("DeliveryMethod");
                HttpContext.Session.Remove("PaymentMethod");
                HttpContext.Session.Remove("ShippingFee");

                var ids = string.Join(",", createdOrderIds);

                if (selectedPaymentMethod == PaymentMethodType.CashOnDelivery)
                {
                    return RedirectToAction("Confirmation", "Order", new { ids = ids });
                }

                return RedirectToAction("Gateway", "Payment", new { method = paymentMethod, orderIds = ids });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = $"Order processing failed: {ex.Message}";
                return RedirectToAction(nameof(Payment));
            }
        });
    }

    [HttpPost("delete-address")]
    public async Task<IActionResult> DeleteAddress(int id)
    {
        var userId = GetUserId();
        var address = await _db.Addresses.FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);
        if (address != null)
        {
            _db.Addresses.Remove(address);
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Address));
    }

    private static bool TryResolvePaymentMethod(string? method, out PaymentMethodType paymentMethod)
    {
        switch (method?.Trim().ToLowerInvariant())
        {
            case "bkash":
                paymentMethod = PaymentMethodType.BkashPersonal;
                return true;
            case "nagad":
                paymentMethod = PaymentMethodType.Nagad;
                return true;
            case "rocket":
                paymentMethod = PaymentMethodType.Rocket;
                return true;
            case "card":
                paymentMethod = PaymentMethodType.CreditCard;
                return true;
            case "instalment":
                paymentMethod = PaymentMethodType.Instalment;
                return true;
            case "cod":
                paymentMethod = PaymentMethodType.CashOnDelivery;
                return true;
            default:
                paymentMethod = default;
                return false;
        }
    }

    public class CheckoutAddressModel
    {
        public string FullName { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Line1 { get; set; } = string.Empty;
        public string? Line2 { get; set; }
        public string City { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
        public string? Area { get; set; }
        public string PostalCode { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class AddressStepViewModel
    {
        public Cart Cart { get; set; } = default!;
        public List<Address> SavedAddresses { get; set; } = new();
        public CheckoutAddressModel NewAddress { get; set; } = default!;
        public decimal Subtotal => Cart.Items.Sum(i => i.UnitPrice * i.Quantity);
    }

    public class DeliveryStepViewModel
    {
        public Cart Cart { get; set; } = default!;
        public decimal Subtotal => Cart.Items.Sum(i => i.UnitPrice * i.Quantity);
    }

    public class PaymentStepViewModel
    {
        public Cart Cart { get; set; } = default!;
        public decimal Subtotal => Cart.Items.Sum(i => i.UnitPrice * i.Quantity);
        public decimal ShippingFee { get; set; }
    }
}
