using Microsoft.EntityFrameworkCore;
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

    public PromotionEvaluator(
        IPromotionRepository promotions,
        ICouponCodeRepository coupons,
        ICouponRedemptionRepository redemptions,
        IPromotionsUnitOfWork uow,
        TimeProvider timeProvider)
    {
        _promotions = promotions;
        _coupons = coupons;
        _redemptions = redemptions;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<(CouponQuote? Quote, CouponError? Error)> QuoteAsync(string code, Guid userId, decimal subtotal, CancellationToken ct = default)
    {
        var normalizedCode = Normalize(code);
        var coupon = await _coupons.GetByCodeAsync(normalizedCode, ct);
        if (coupon is null) return (null, NotExistsError());

        var promotion = await _promotions.GetByIdAsync(coupon.PromotionId, ct);
        if (promotion is null) return (null, NotExistsError());

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var error = await ValidateAsync(coupon, promotion, userId, subtotal, now, ct);
        if (error is not null) return (null, error);

        var discount = promotion.CalculateDiscount(subtotal);
        return (new CouponQuote(coupon.Code, promotion.Id, promotion.Name, discount), null);
    }

    public async Task<(CouponQuote? Quote, CouponError? Error)> ReserveAsync(string code, Guid userId, Guid orderId, decimal subtotal, CancellationToken ct = default)
    {
        var existing = await _redemptions.GetByOrderIdAsync(orderId, ct);
        if (existing is not null && existing.Status != RedemptionStatus.Released)
        {
            var existingPromotion = await _promotions.GetByIdAsync(existing.PromotionId, ct);
            var existingCoupon = await _coupons.GetByIdAsync(existing.CouponCodeId, ct);
            return (new CouponQuote(existingCoupon?.Code ?? code, existing.PromotionId, existingPromotion?.Name ?? string.Empty, existing.DiscountAmount), null);
        }

        var normalizedCode = Normalize(code);
        var coupon = await _coupons.GetByCodeAsync(normalizedCode, ct);
        if (coupon is null) return (null, NotExistsError());

        var promotion = await _promotions.GetByIdAsync(coupon.PromotionId, ct);
        if (promotion is null) return (null, NotExistsError());

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var error = await ValidateAsync(coupon, promotion, userId, subtotal, now, ct);
            if (error is not null) return (null, error);

            var discount = promotion.CalculateDiscount(subtotal);
            promotion.IncrementUsage();
            coupon.IncrementUsage();

            if (attempt == 1)
            {
                var redemption = CouponRedemption.Create(promotion.Id, coupon.Id, orderId, userId, discount, now);
                await _redemptions.AddAsync(redemption, ct);
            }

            _promotions.Update(promotion);
            _coupons.Update(coupon);

            try
            {
                await _uow.SaveChangesAsync(ct);
                return (new CouponQuote(coupon.Code, promotion.Id, promotion.Name, discount), null);
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

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();

    private static CouponError NotExistsError() => ToCouponError(PromotionsErrors.CouponNotExists);

    private static CouponError ToCouponError(Error error) => new(error.Code, error.Message);
}
