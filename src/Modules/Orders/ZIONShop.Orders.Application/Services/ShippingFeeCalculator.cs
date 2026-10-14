using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Orders;
using ZIONShop.Orders.Application.Options;

namespace ZIONShop.Orders.Application.Services;

public class ShippingFeeCalculator : IShippingFeeCalculator
{
    private readonly CheckoutOptions _checkout;

    public ShippingFeeCalculator(IOptions<CheckoutOptions> checkout) => _checkout = checkout.Value;

    public decimal Calculate(decimal subtotal)
        => subtotal >= _checkout.FreeShippingThreshold ? 0m : _checkout.ShippingFee;
}
