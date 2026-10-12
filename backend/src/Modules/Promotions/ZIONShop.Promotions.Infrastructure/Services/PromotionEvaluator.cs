using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Infrastructure.Services;

/// <summary>
/// Implements the Contracts-facing coupon evaluator. Reserve/Release increment or decrement the
/// atomic UsedCount counters on <see cref="Promotion"/>/<see cref="CouponCode"/> guarded by the
/// PostgreSQL xmin concurrency token; a concurrent reservation that loses the race gets its stale
/// in-memory state reloaded from the database and the whole check-then-mutate step retried.
/// </summary>
public class PromotionEvaluator : IPromotionEvaluator
{
    private const int MaxAttempts = 3;

    private readonly IPromotionRepository _promotions;
    private readonly ICouponCodeRepository _coupons;
    private readonly ICouponRedemptionRepository _redemptions;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly IProductCatalogReader _catalogReader;

    public PromotionEvaluator(
        IPromotionRepository promotions,
        ICouponCodeRepository coupons,
        ICouponRedemptionRepository redemptions,
        IPromotionsUnitOfWork uow,
        TimeProvider timeProvider,
        IProductCatalogReader catalogReader)
    {
        _promotions = promotions;
        _coupons = coupons;
        _redemptions = redemptions;
        _uow = uow;
        _timeProvider = timeProvider;
        _catalogReader = catalogReader;
    }

    public async Task<(CouponQuote? Quote, CouponError? Error)> QuoteAsync(CouponRequest request, CancellationToken ct = default)
    {
        var normalizedCode = Normalize(request.Code);
        var coupon = await _coupons.GetByCodeAsync(normalizedCode, ct);
        if (coupon is null) return (null, NotExistsError());

        var promotion = await _promotions.GetByIdAsync(coupon.PromotionId, ct);
        if (promotion is null) return (null, NotExistsError());

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var error = await ValidateAsync(coupon, promotion, request.UserId, request.Subtotal, now, ct);
        if (error is not null) return (null, error);

        var (discount, target, discountError) = await ComputeDiscountAsync(promotion, request, ct);
        if (discountError is not null) return (null, discountError);

        return (new CouponQuote(coupon.Code, promotion.Id, promotion.Name, discount, target), null);
    }

    public async Task<(CouponQuote? Quote, CouponError? Error)> ReserveAsync(CouponRequest request, Guid orderId, CancellationToken ct = default)
    {
        var existing = await _redemptions.GetByOrderIdAsync(orderId, ct);
        if (existing is not null && existing.Status != RedemptionStatus.Released)
        {
            var existingPromotion = await _promotions.GetByIdAsync(existing.PromotionId, ct);
            var existingCoupon = await _coupons.GetByIdAsync(existing.CouponCodeId, ct);
            var existingTarget = existingPromotion?.Type == PromotionType.FreeShipping ? "Shipping" : "Items";
            return (new CouponQuote(existingCoupon?.Code ?? request.Code, existing.PromotionId, existingPromotion?.Name ?? string.Empty, existing.DiscountAmount, existingTarget), null);
        }

        var normalizedCode = Normalize(request.Code);
        var coupon = await _coupons.GetByCodeAsync(normalizedCode, ct);
        if (coupon is null) return (null, NotExistsError());

        var promotion = await _promotions.GetByIdAsync(coupon.PromotionId, ct);
        if (promotion is null) return (null, NotExistsError());

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var error = await ValidateAsync(coupon, promotion, request.UserId, request.Subtotal, now, ct);
            if (error is not null) return (null, error);

            var (discount, target, discountError) = await ComputeDiscountAsync(promotion, request, ct);
            if (discountError is not null) return (null, discountError);

            promotion.IncrementUsage();
            coupon.IncrementUsage();

            if (attempt == 1)
            {
                var redemption = CouponRedemption.Create(promotion.Id, coupon.Id, orderId, request.UserId, discount, now);
                await _redemptions.AddAsync(redemption, ct);
            }

            _promotions.Update(promotion);
            _coupons.Update(coupon);

            try
            {
                await _uow.SaveChangesAsync(ct);
                return (new CouponQuote(coupon.Code, promotion.Id, promotion.Name, discount, target), null);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entry in ex.Entries)
                    await entry.ReloadAsync(ct);
            }
        }

        return (null, ToCouponError(PromotionsErrors.CouponUsageLimitReached));
    }

    public async Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        var redemption = await _redemptions.GetByOrderIdAsync(orderId, ct);
        if (redemption is null || redemption.Status == RedemptionStatus.Released) return;

        var promotion = await _promotions.GetByIdAsync(redemption.PromotionId, ct);
        var coupon = await _coupons.GetByIdAsync(redemption.CouponCodeId, ct);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            promotion?.DecrementUsage();
            coupon?.DecrementUsage();
            redemption.MarkReleased(_timeProvider.GetUtcNow().UtcDateTime);

            if (promotion is not null) _promotions.Update(promotion);
            if (coupon is not null) _coupons.Update(coupon);
            _redemptions.Update(redemption);

            try
            {
                await _uow.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entry in ex.Entries)
                    await entry.ReloadAsync(ct);
            }
        }
    }

    private async Task<CouponError?> ValidateAsync(CouponCode coupon, Promotion promotion, Guid userId, decimal subtotal, DateTime now, CancellationToken ct)
    {
        if (!coupon.IsActive || !promotion.IsActive)
            return ToCouponError(PromotionsErrors.CouponInactive);

        if (now < promotion.StartsAt)
            return ToCouponError(PromotionsErrors.CouponNotStarted);

        if (now > promotion.EndsAt)
            return ToCouponError(PromotionsErrors.CouponExpired);

        if (subtotal < promotion.MinOrderSubtotal)
            return ToCouponError(PromotionsErrors.CouponMinSubtotalNotMet(promotion.MinOrderSubtotal));

        if (promotion.UsageLimit.HasValue && promotion.UsedCount >= promotion.UsageLimit.Value)
            return ToCouponError(PromotionsErrors.CouponUsageLimitReached);

        if (promotion.PerUserLimit.HasValue)
        {
            var userUsage = await _redemptions.CountActiveForUserAsync(promotion.Id, userId, ct);
            if (userUsage >= promotion.PerUserLimit.Value)
                return ToCouponError(PromotionsErrors.CouponPerUserLimitReached);
        }

        return null;
    }

    /// <summary>
    /// Resolves the eligible base amount for Scope (Order = the whole order's Subtotal, independent of
    /// Lines so callers that only pass Subtotal keep working; Products/Categories = sum of matching
    /// CouponLines) then computes the discount for the promotion's Type.
    /// </summary>
    private async Task<(decimal Discount, string Target, CouponError? Error)> ComputeDiscountAsync(Promotion promotion, CouponRequest request, CancellationToken ct)
    {
        var (eligibleBase, hasEligible) = await ResolveEligibleBaseAsync(promotion, request, ct);
        if (!hasEligible)
            return (0m, string.Empty, ToCouponError(PromotionsErrors.CouponNotApplicable));

        if (promotion.Type == PromotionType.FreeShipping)
        {
            if (request.ShippingFee <= 0m)
                return (0m, string.Empty, ToCouponError(PromotionsErrors.CouponAlreadyFreeShipping));
            return (request.ShippingFee, "Shipping", null);
        }

        return (promotion.CalculateDiscount(eligibleBase), "Items", null);
    }

    private async Task<(decimal EligibleBase, bool HasEligible)> ResolveEligibleBaseAsync(Promotion promotion, CouponRequest request, CancellationToken ct)
    {
        if (promotion.Scope == PromotionScope.Order)
            return (request.Subtotal, true);

        var categoryCache = new Dictionary<Guid, Guid?>();
        decimal eligibleBase = 0m;
        var hasEligible = false;

        foreach (var line in request.Lines)
        {
            Guid? categoryId = null;
            if (promotion.Scope == PromotionScope.Categories)
            {
                if (!categoryCache.TryGetValue(line.ProductId, out categoryId))
                {
                    categoryId = (await _catalogReader.GetAsync(line.ProductId, ct))?.CategoryId;
                    categoryCache[line.ProductId] = categoryId;
                }
            }

            if (!promotion.IsLineEligible(line.ProductId, categoryId)) continue;

            eligibleBase += line.LineTotal;
            hasEligible = true;
        }

        return (eligibleBase, hasEligible);
    }

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();

    private static CouponError NotExistsError() => ToCouponError(PromotionsErrors.CouponNotExists);

    private static CouponError ToCouponError(Error error) => new(error.Code, error.Message);
}
