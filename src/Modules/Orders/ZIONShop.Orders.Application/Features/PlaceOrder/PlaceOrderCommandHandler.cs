using MediatR;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Cart;
using ZIONShop.Contracts.Inventory;
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
    private readonly IGiftPromotionEvaluator _giftPromotions;
    private readonly IStockAvailabilityReader _stockAvailability;
    private readonly IShippingFeeCalculator _shippingFeeCalculator;
    private readonly CheckoutOptions _checkout;

    public PlaceOrderCommandHandler(
        IOrderRepository orders,
        IOrdersUnitOfWork uow,
        ICartReader cartReader,
        IUserAddressReader addresses,
        IProductPriceLookup prices,
        IPromotionEvaluator promotions,
        IGiftPromotionEvaluator giftPromotions,
        IStockAvailabilityReader stockAvailability,
        IShippingFeeCalculator shippingFeeCalculator,
        IOptions<CheckoutOptions> checkout)
    {
        _orders = orders;
        _uow = uow;
        _cartReader = cartReader;
        _addresses = addresses;
        _prices = prices;
        _promotions = promotions;
        _giftPromotions = giftPromotions;
        _stockAvailability = stockAvailability;
        _shippingFeeCalculator = shippingFeeCalculator;
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
        var shippingFee = _shippingFeeCalculator.Calculate(subtotal);

        var shippingAddress = new ShippingAddress(
            address.AddressId, address.RecipientName, address.PhoneNumber, address.Province, address.Ward, address.StreetAddress);

        var order = DomainOrder.Place(
            request.UserId, paymentMethod, currency ?? _checkout.Currency, shippingFee, shippingAddress, lines, request.Note);

        var hasCoupon = !string.IsNullOrWhiteSpace(request.CouponCode);
        if (hasCoupon)
        {
            var couponLines = lines.Select(l => new CouponLine(l.ProductId, l.Quantity, l.UnitPrice * l.Quantity)).ToList();
            var couponRequest = new CouponRequest(request.CouponCode!, request.UserId, couponLines, subtotal, shippingFee);
            var (quote, error) = await _promotions.ReserveAsync(couponRequest, order.Id, cancellationToken);
            if (error is not null)
                return Result.Failure<OrderDto>(OrdersErrors.InvalidCoupon(error.Message));

            order.ApplyCoupon(quote!.Code, quote.DiscountAmount, quote.DiscountTarget);
        }

        var giftLines = lines.Select(l => new CouponLine(l.ProductId, l.Quantity, l.UnitPrice * l.Quantity)).ToList();
        var giftRequest = new GiftRequest(request.UserId, giftLines, subtotal);
        var giftGrants = await _giftPromotions.QuoteGiftsAsync(giftRequest, cancellationToken);

        var keptPromotionIds = new List<Guid>();
        var keptProductIds = new HashSet<Guid>();
        if (giftGrants.Count > 0)
        {
            var neededByProduct = giftGrants
                .GroupBy(g => g.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            foreach (var line in lines)
                if (neededByProduct.ContainsKey(line.ProductId))
                    neededByProduct[line.ProductId] += line.Quantity;

            var available = await _stockAvailability.GetAvailableAsync(neededByProduct.Keys.ToList(), cancellationToken);
            keptProductIds = neededByProduct
                .Where(kv => (available.TryGetValue(kv.Key, out var a) ? a : 0) >= kv.Value)
                .Select(kv => kv.Key)
                .ToHashSet();

            keptPromotionIds = giftGrants
                .Where(g => keptProductIds.Contains(g.ProductId))
                .Select(g => g.PromotionId)
                .Distinct()
                .ToList();
        }

        var hasGifts = keptPromotionIds.Count > 0;
        if (hasGifts)
        {
            var reservedGrants = await _giftPromotions.ReserveGiftsAsync(giftRequest, order.Id, keptPromotionIds, cancellationToken);
            if (reservedGrants.Count > 0)
            {
                var giftPrices = await _prices.GetManyAsync(reservedGrants.Select(g => g.ProductId).Distinct().ToList(), cancellationToken);
                foreach (var grant in reservedGrants)
                {
                    if (keptProductIds.Contains(grant.ProductId) && giftPrices.TryGetValue(grant.ProductId, out var info))
                        order.AddGiftLine(grant.ProductId, info.Name, grant.Quantity, grant.PromotionId);
                }
            }
        }

        try
        {
            await _orders.AddAsync(order, cancellationToken);

            _uow.AddOutboxEvent(new OrderCreatedIntegrationEvent(
                order.Id,
                order.UserId,
                order.Total,
                order.Lines.Select(l => new OrderLine(l.ProductId, l.Quantity, l.UnitPrice, l.IsGift)).ToList(),
                order.PaymentMethod.ToString(),
                order.Currency));

            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (hasCoupon)
                await _promotions.ReleaseAsync(order.Id, cancellationToken);
            if (hasGifts)
                await _giftPromotions.ReleaseGiftsAsync(order.Id, cancellationToken);
            throw;
        }

        return Result.Success(order.ToDto());
    }
}
