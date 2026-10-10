using MediatR;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Cart;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Contracts.Users;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Application.Options;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.SharedKernel.Results;
using DomainOrder = ZIONShop.Orders.Domain.Entities.Order;
using DomainPaymentMethod = ZIONShop.Orders.Domain.Enums.PaymentMethod;

namespace ZIONShop.Orders.Application.Features.PlaceOrder;

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Result<OrderDto>>
{
    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;
    private readonly ICartReader _cartReader;
    private readonly IUserAddressReader _addresses;
    private readonly IProductPriceLookup _prices;
    private readonly IPromotionEvaluator _promotions;
    private readonly CheckoutOptions _checkout;

    public PlaceOrderCommandHandler(
        IOrderRepository orders,
        IOrdersUnitOfWork uow,
        ICartReader cartReader,
        IUserAddressReader addresses,
        IProductPriceLookup prices,
        IPromotionEvaluator promotions,
        IOptions<CheckoutOptions> checkout)
    {
        _orders = orders;
        _uow = uow;
        _cartReader = cartReader;
        _addresses = addresses;
        _prices = prices;
        _promotions = promotions;
        _checkout = checkout.Value;
    }

    public async Task<Result<OrderDto>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var cart = await _cartReader.GetForUserAsync(request.UserId, cancellationToken);
        if (cart is null || cart.Lines.Count == 0)
            return Result.Failure<OrderDto>(OrdersErrors.CartEmpty);

        var address = await _addresses.GetAsync(request.UserId, request.AddressId, cancellationToken);
        if (address is null)
            return Result.Failure<OrderDto>(OrdersErrors.AddressNotFound);

        if (!Enum.TryParse<DomainPaymentMethod>(request.PaymentMethod, ignoreCase: true, out var paymentMethod))
            return Result.Failure<OrderDto>(OrdersErrors.InvalidPaymentMethod);

        var lines = new List<(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity)>();
        var unavailable = new List<string>();
        string? currency = null;

        foreach (var line in cart.Lines)
        {
            var price = await _prices.GetAsync(line.ProductId, cancellationToken);
            if (price is null || !price.IsAvailable)
            {
                unavailable.Add(line.ProductName);
                continue;
            }

            currency ??= price.Currency;
            lines.Add((line.ProductId, price.Name, price.UnitPrice, line.Quantity));
        }

        if (unavailable.Count > 0)
            return Result.Failure<OrderDto>(OrdersErrors.ProductUnavailable(string.Join(", ", unavailable)));

        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var shippingFee = subtotal >= _checkout.FreeShippingThreshold ? 0m : _checkout.ShippingFee;

        var shippingAddress = new ShippingAddress(
            address.AddressId, address.RecipientName, address.PhoneNumber, address.Province, address.Ward, address.StreetAddress);

        var order = DomainOrder.Place(
            request.UserId, paymentMethod, currency ?? _checkout.Currency, shippingFee, shippingAddress, lines, request.Note);

        var hasCoupon = !string.IsNullOrWhiteSpace(request.CouponCode);
        if (hasCoupon)
        {
            var (quote, error) = await _promotions.ReserveAsync(request.CouponCode!, request.UserId, order.Id, subtotal, cancellationToken);
            if (error is not null)
                return Result.Failure<OrderDto>(OrdersErrors.InvalidCoupon(error.Message));

            order.ApplyCoupon(quote!.Code, quote.DiscountAmount);
        }

        try
        {
            await _orders.AddAsync(order, cancellationToken);

            _uow.AddOutboxEvent(new OrderCreatedIntegrationEvent(
                order.Id,
                order.UserId,
                order.Total,
                order.Lines.Select(l => new OrderLine(l.ProductId, l.Quantity, l.UnitPrice)).ToList(),
                order.PaymentMethod.ToString(),
                order.Currency));

            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (hasCoupon)
                await _promotions.ReleaseAsync(order.Id, cancellationToken);
            throw;
        }

        return Result.Success(order.ToDto());
    }
}
