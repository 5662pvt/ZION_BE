using System.Text.RegularExpressions;
using MediatR;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Cart;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Contracts.Orders;
using ZIONShop.Contracts.Products;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Contracts.Users;
using ZIONShop.Orders.Application.Exceptions;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Application.Options;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.Orders.Domain.ValueObjects;
using ZIONShop.SharedKernel.Results;
using ContractsOrderLine = ZIONShop.Contracts.Orders.OrderLine;
using DomainOrder = ZIONShop.Orders.Domain.Entities.Order;
using DomainPaymentMethod = ZIONShop.Orders.Domain.Enums.PaymentMethod;

namespace ZIONShop.Orders.Application.Features.PlaceOrder;

public class PlaceOrderCommandHandler : IRequestHandler<PlaceOrderCommand, Result<PlaceOrderResult>>
{
    private static readonly Regex IdempotencyKeyPattern = new("^[A-Za-z0-9_\\-:.]{1,100}$", RegexOptions.Compiled);

    private readonly IOrderRepository _orders;
    private readonly IOrdersUnitOfWork _uow;
    private readonly ICartReader _cartReader;
    private readonly IUserAddressReader _addresses;
    private readonly IProductPriceLookup _prices;
    private readonly IPromotionEvaluator _promotions;
    private readonly IGiftPromotionEvaluator _giftPromotions;
    private readonly IFlashSaleEvaluator _flashSales;
    private readonly IStockAvailabilityReader _stockAvailability;
    private readonly IShippingFeeCalculator _shippingFeeCalculator;
    private readonly IOrderIdempotencyKeyRepository _idempotencyKeys;
    private readonly IIdempotencyLock _idempotencyLock;
    private readonly TimeProvider _timeProvider;
    private readonly CheckoutOptions _checkout;

    public PlaceOrderCommandHandler(
        IOrderRepository orders,
        IOrdersUnitOfWork uow,
        ICartReader cartReader,
        IUserAddressReader addresses,
        IProductPriceLookup prices,
        IPromotionEvaluator promotions,
        IGiftPromotionEvaluator giftPromotions,
        IFlashSaleEvaluator flashSales,
        IStockAvailabilityReader stockAvailability,
        IShippingFeeCalculator shippingFeeCalculator,
        IOrderIdempotencyKeyRepository idempotencyKeys,
        IIdempotencyLock idempotencyLock,
        TimeProvider timeProvider,
        IOptions<CheckoutOptions> checkout)
    {
        _orders = orders;
        _uow = uow;
        _cartReader = cartReader;
        _addresses = addresses;
        _prices = prices;
        _promotions = promotions;
        _giftPromotions = giftPromotions;
        _flashSales = flashSales;
        _stockAvailability = stockAvailability;
        _shippingFeeCalculator = shippingFeeCalculator;
        _idempotencyKeys = idempotencyKeys;
        _idempotencyLock = idempotencyLock;
        _timeProvider = timeProvider;
        _checkout = checkout.Value;
    }

    public async Task<Result<PlaceOrderResult>> Handle(PlaceOrderCommand request, CancellationToken cancellationToken)
    {
        var hasIdempotencyKey = !string.IsNullOrEmpty(request.IdempotencyKey);
        string? fingerprint = null;

        if (hasIdempotencyKey && !IdempotencyKeyPattern.IsMatch(request.IdempotencyKey!))
            return Result.Failure<PlaceOrderResult>(OrdersErrors.InvalidIdempotencyKey);

        // Serializes concurrent requests for the same (user, key) BEFORE any side effect (coupon/gift
        // reservation, order insert) runs, so a losing request's replay lookup below always finds the
        // winner's already-committed row instead of racing it -- the unique-index catch further down
        // remains as a fallback.
        await using var idempotencyLock = hasIdempotencyKey
            ? await _idempotencyLock.AcquireAsync(request.UserId, request.IdempotencyKey!, cancellationToken)
            : null;

        if (hasIdempotencyKey)
        {
            if (idempotencyLock is null)
                return Result.Failure<PlaceOrderResult>(OrdersErrors.IdempotencyKeyInProgress);

            fingerprint = IdempotencyFingerprint.Compute(request.AddressId, request.PaymentMethod, request.Note, request.CouponCode);

            var existingKey = await _idempotencyKeys.GetAsync(request.UserId, request.IdempotencyKey!, cancellationToken);
            var replay = await TryReplayAsync(existingKey, fingerprint, cancellationToken);
            if (replay is not null) return replay;
        }

        var cart = await _cartReader.GetForUserAsync(request.UserId, cancellationToken);
        if (cart is null || cart.Lines.Count == 0)
            return Result.Failure<PlaceOrderResult>(OrdersErrors.CartEmpty);

        var address = await _addresses.GetAsync(request.UserId, request.AddressId, cancellationToken);
        if (address is null)
            return Result.Failure<PlaceOrderResult>(OrdersErrors.AddressNotFound);

        if (!Enum.TryParse<DomainPaymentMethod>(request.PaymentMethod, ignoreCase: true, out var paymentMethod))
            return Result.Failure<PlaceOrderResult>(OrdersErrors.InvalidPaymentMethod);

        // Read-only preview: decides per-line pricing now (OrderLine has no post-creation price mutator,
        // so the flash price must be baked in before DomainOrder.Place builds its lines). The authoritative
        // atomic quota claim happens after Place, alongside the coupon/gift reservations, once order.Id
        // exists -- same quote-before/reserve-after split already used for gifts in this handler. A line
        // whose quota is lost to a concurrent order in that window keeps its already-baked flash price;
        // this mirrors the gift flow's same quote/reserve race and is accepted for the same reason.
        var flashSaleLines = cart.Lines.Select(l => new FlashSaleLine(l.ProductId, l.Quantity, l.VariantId ?? l.ProductId)).ToList();
        var flashQuotes = await _flashSales.QuoteAsync(flashSaleLines, cancellationToken);
        var flashPriceByVariant = flashQuotes.ToDictionary(q => q.VariantId ?? q.ProductId, q => q.FlashUnitPrice);

        var lines = new List<OrderLineInput>();
        var unavailable = new List<string>();
        string? currency = null;

        foreach (var line in cart.Lines)
        {
            var variantKey = line.VariantId ?? line.ProductId;
            var price = await _prices.GetAsync(variantKey, cancellationToken);
            if (price is null || !price.IsAvailable)
            {
                unavailable.Add(price is null ? line.ProductName : FormatProductLabel(price.Name, price.VariantLabel));
                continue;
            }

            currency ??= price.Currency;
            var resolvedVariantId = price.VariantId ?? price.ProductId;
            var unitPrice = flashPriceByVariant.TryGetValue(resolvedVariantId, out var flashPrice) ? flashPrice : price.UnitPrice;
            lines.Add(new OrderLineInput(price.ProductId, price.Name, unitPrice, line.Quantity, resolvedVariantId, price.VariantLabel, price.Sku));
        }

        if (unavailable.Count > 0)
            return Result.Failure<PlaceOrderResult>(OrdersErrors.ProductUnavailable(string.Join(", ", unavailable)));

        var insufficientStockNames = await GetInsufficientStockProductNamesAsync(lines, cancellationToken);
        if (insufficientStockNames.Count > 0)
            return Result.Failure<PlaceOrderResult>(OrdersErrors.InsufficientStock(string.Join(", ", insufficientStockNames)));

        var subtotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var shippingFee = _shippingFeeCalculator.Calculate(subtotal);

        var shippingAddress = new ShippingAddress(
            address.AddressId, address.RecipientName, address.PhoneNumber, address.Province, address.Ward, address.StreetAddress);

        var order = DomainOrder.Place(
            request.UserId, paymentMethod, currency ?? _checkout.Currency, shippingFee, shippingAddress, lines, request.Note);

        var hasFlashSale = flashQuotes.Count > 0;
        if (hasFlashSale)
            await _flashSales.ReserveAsync(flashSaleLines, order.Id, request.UserId, cancellationToken);

        var hasCoupon = !string.IsNullOrWhiteSpace(request.CouponCode);
        if (hasCoupon)
        {
            var couponLines = lines.Select(l => new CouponLine(l.ProductId, l.Quantity, l.UnitPrice * l.Quantity)).ToList();
            var couponRequest = new CouponRequest(request.CouponCode!, request.UserId, couponLines, subtotal, shippingFee);
            var (quote, error) = await _promotions.ReserveAsync(couponRequest, order.Id, cancellationToken);
            if (error is not null)
                return Result.Failure<PlaceOrderResult>(OrdersErrors.InvalidCoupon(error.Message));

            order.ApplyCoupon(quote!.Code, quote.DiscountAmount, quote.DiscountTarget);
        }

        var giftLines = lines.Select(l => new CouponLine(l.ProductId, l.Quantity, l.UnitPrice * l.Quantity)).ToList();
        var giftRequest = new GiftRequest(request.UserId, giftLines, subtotal);
        var giftGrants = await _giftPromotions.QuoteGiftsAsync(giftRequest, cancellationToken);

        var keptPromotionIds = new List<Guid>();
        var keptVariantIds = new HashSet<Guid>();
        if (giftGrants.Count > 0)
        {
            var neededByVariant = giftGrants
                .GroupBy(g => g.VariantId ?? g.ProductId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
            foreach (var line in lines)
                if (neededByVariant.ContainsKey(line.ResolvedVariantId))
                    neededByVariant[line.ResolvedVariantId] += line.Quantity;

            var available = await _stockAvailability.GetAvailableAsync(neededByVariant.Keys.ToList(), cancellationToken);
            keptVariantIds = neededByVariant
                .Where(kv => (available.TryGetValue(kv.Key, out var a) ? a : 0) >= kv.Value)
                .Select(kv => kv.Key)
                .ToHashSet();

            keptPromotionIds = giftGrants
                .Where(g => keptVariantIds.Contains(g.VariantId ?? g.ProductId))
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
                var giftPrices = await _prices.GetManyAsync(reservedGrants.Select(g => g.VariantId ?? g.ProductId).Distinct().ToList(), cancellationToken);
                foreach (var grant in reservedGrants)
                {
                    var grantKey = grant.VariantId ?? grant.ProductId;
                    if (keptVariantIds.Contains(grantKey) && giftPrices.TryGetValue(grantKey, out var info))
                        order.AddGiftLine(grant.ProductId, info.Name, grant.Quantity, grant.PromotionId, grantKey, info.VariantLabel, info.Sku);
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
                order.Lines.Select(l => new ContractsOrderLine(l.ProductId, l.Quantity, l.UnitPrice, l.IsGift, l.VariantId)).ToList(),
                order.PaymentMethod.ToString(),
                order.Currency));

            if (hasIdempotencyKey)
            {
                var keyRow = OrderIdempotencyKey.Create(
                    request.UserId, request.IdempotencyKey!, fingerprint!, order.Id, _timeProvider.GetUtcNow().UtcDateTime);
                _idempotencyKeys.Add(keyRow);
            }

            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateIdempotencyKeyException)
        {
            // Lost the race on the (UserId, Key) unique index: a concurrent request for the same key already
            // committed first. Release what we speculatively reserved, then replay the winner's order.
            if (hasCoupon)
                await _promotions.ReleaseAsync(order.Id, cancellationToken);
            if (hasGifts)
                await _giftPromotions.ReleaseGiftsAsync(order.Id, cancellationToken);
            if (hasFlashSale)
                await _flashSales.ReleaseAsync(order.Id, cancellationToken);

            var winner = await _idempotencyKeys.GetAsync(request.UserId, request.IdempotencyKey!, cancellationToken);
            var replay = await TryReplayAsync(winner, fingerprint!, cancellationToken);
            return replay ?? Result.Failure<PlaceOrderResult>(OrdersErrors.IdempotencyKeyReused);
        }
        catch
        {
            if (hasCoupon)
                await _promotions.ReleaseAsync(order.Id, cancellationToken);
            if (hasGifts)
                await _giftPromotions.ReleaseGiftsAsync(order.Id, cancellationToken);
            if (hasFlashSale)
                await _flashSales.ReleaseAsync(order.Id, cancellationToken);
            throw;
        }

        return Result.Success(new PlaceOrderResult(order.ToDto(), Replayed: false));
    }

    /// <summary>Returns a replay/conflict result if <paramref name="existingKey"/> already settles this
    /// Idempotency-Key, or null if the caller should proceed to place a new order.</summary>
    private async Task<Result<PlaceOrderResult>?> TryReplayAsync(OrderIdempotencyKey? existingKey, string fingerprint, CancellationToken cancellationToken)
    {
        if (existingKey is null) return null;

        if (existingKey.RequestHash != fingerprint)
            return Result.Failure<PlaceOrderResult>(OrdersErrors.IdempotencyKeyReused);

        var existingOrder = await _orders.GetByIdAsync(existingKey.OrderId, cancellationToken);
        if (existingOrder is null) return Result.Failure<PlaceOrderResult>(OrdersErrors.IdempotencyKeyReused);

        return Result.Success(new PlaceOrderResult(existingOrder.ToDto(), Replayed: true));
    }

    private async Task<List<string>> GetInsufficientStockProductNamesAsync(
        List<OrderLineInput> lines,
        CancellationToken cancellationToken)
    {
        var variantIds = lines.Select(l => l.ResolvedVariantId).Distinct().ToList();
        var available = await _stockAvailability.GetAvailableAsync(variantIds, cancellationToken);

        return lines
            .GroupBy(l => l.ResolvedVariantId)
            .Where(g => (available.TryGetValue(g.Key, out var a) ? a : 0) < g.Sum(l => l.Quantity))
            .Select(g => FormatProductLabel(g.First().ProductName, g.First().VariantLabel))
            .ToList();
    }

    private static string FormatProductLabel(string productName, string? variantLabel) =>
        string.IsNullOrEmpty(variantLabel) ? productName : $"{productName} – {variantLabel}";
}
