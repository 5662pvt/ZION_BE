using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Repositories;

namespace ZIONShop.Promotions.Infrastructure.Services;

/// <summary>
/// Implements the Contracts-facing gift evaluator. Mirrors <see cref="PromotionEvaluator"/>'s xmin
/// optimistic-concurrency retry on Reserve/Release, but a request can qualify for several Gift
/// promotions at once, so Reserve/Release operate per promotionId and return/release one GiftGrant
/// set (one grant per configured gift item) per promotion.
/// </summary>
public class GiftPromotionEvaluator : IGiftPromotionEvaluator
{
    private const int MaxAttempts = 3;

    private readonly IPromotionRepository _promotions;
    private readonly IGiftRedemptionRepository _redemptions;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;
    private readonly IProductCatalogReader _catalogReader;

    public GiftPromotionEvaluator(
        IPromotionRepository promotions,
        IGiftRedemptionRepository redemptions,
        IPromotionsUnitOfWork uow,
        TimeProvider timeProvider,
        IProductCatalogReader catalogReader)
    {
        _promotions = promotions;
        _redemptions = redemptions;
        _uow = uow;
        _timeProvider = timeProvider;
        _catalogReader = catalogReader;
    }

    public async Task<IReadOnlyList<GiftGrant>> QuoteGiftsAsync(GiftRequest request, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var candidates = await GetCandidatePromotionsAsync(now, ct);

        var grants = new List<GiftGrant>();
        foreach (var promotion in candidates)
        {
            if (await QualifiesAsync(promotion, request, now, ct))
                grants.AddRange(ToGrants(promotion));
        }

        return grants;
    }

    public async Task<IReadOnlyList<GiftGrant>> ReserveGiftsAsync(GiftRequest request, Guid orderId, IReadOnlyCollection<Guid> promotionIds, CancellationToken ct = default)
    {
        var grants = new List<GiftGrant>();
        foreach (var promotionId in promotionIds.Distinct())
            grants.AddRange(await ReserveOneAsync(request, orderId, promotionId, ct));

        return grants;
    }

    public async Task ReleaseGiftsAsync(Guid orderId, CancellationToken ct = default)
    {
        var redemptions = await _redemptions.GetByOrderIdAsync(orderId, ct);
        foreach (var redemption in redemptions.Where(r => r.Status != RedemptionStatus.Released))
            await ReleaseOneRedemptionAsync(redemption, ct);
    }

    public async Task ReleaseGiftAsync(Guid orderId, Guid promotionId, CancellationToken ct = default)
    {
        var redemption = await _redemptions.GetByPromotionAndOrderAsync(promotionId, orderId, ct);
        if (redemption is null || redemption.Status == RedemptionStatus.Released) return;
        await ReleaseOneRedemptionAsync(redemption, ct);
    }

    private async Task<IReadOnlyList<GiftGrant>> ReserveOneAsync(GiftRequest request, Guid orderId, Guid promotionId, CancellationToken ct)
    {
        var existing = await _redemptions.GetByPromotionAndOrderAsync(promotionId, orderId, ct);
        if (existing is not null && existing.Status != RedemptionStatus.Released)
        {
            var existingPromotion = await _promotions.GetByIdAsync(promotionId, ct);
            return existingPromotion is null ? Array.Empty<GiftGrant>() : ToGrants(existingPromotion);
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var promotion = await _promotions.GetByIdAsync(promotionId, ct);
            if (promotion is null) return Array.Empty<GiftGrant>();

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            if (!await QualifiesAsync(promotion, request, now, ct))
                return Array.Empty<GiftGrant>();

            promotion.IncrementUsage();

            if (attempt == 1)
            {
                var redemption = GiftRedemption.Create(promotion.Id, orderId, request.UserId, now);
                await _redemptions.AddAsync(redemption, ct);
            }

            _promotions.Update(promotion);

            try
            {
                await _uow.SaveChangesAsync(ct);
                return ToGrants(promotion);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                foreach (var entry in ex.Entries)
                    await entry.ReloadAsync(ct);
            }
        }

        return Array.Empty<GiftGrant>();
    }

    private async Task ReleaseOneRedemptionAsync(GiftRedemption redemption, CancellationToken ct)
    {
        var promotion = await _promotions.GetByIdAsync(redemption.PromotionId, ct);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            promotion?.DecrementUsage();
            redemption.MarkReleased(_timeProvider.GetUtcNow().UtcDateTime);

            if (promotion is not null) _promotions.Update(promotion);
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

    private Task<List<Promotion>> GetCandidatePromotionsAsync(DateTime now, CancellationToken ct) =>
        _promotions.Query().AsNoTracking()
            .Where(p => p.Type == PromotionType.Gift && p.IsActive && p.StartsAt <= now && p.EndsAt >= now)
            .Where(p => p.UsageLimit == null || p.UsedCount < p.UsageLimit)
            .ToListAsync(ct);

    private async Task<bool> QualifiesAsync(Promotion promotion, GiftRequest request, DateTime now, CancellationToken ct)
    {
        if (promotion.Type != PromotionType.Gift) return false;
        if (!promotion.IsActive || now < promotion.StartsAt || now > promotion.EndsAt) return false;
        if (promotion.UsageLimit.HasValue && promotion.UsedCount >= promotion.UsageLimit.Value) return false;

        if (promotion.PerUserLimit.HasValue)
        {
            var userUsage = await _redemptions.CountActiveForUserAsync(promotion.Id, request.UserId, ct);
            if (userUsage >= promotion.PerUserLimit.Value) return false;
        }

        if (request.Subtotal < promotion.MinOrderSubtotal) return false;

        if (promotion.Scope == PromotionScope.Order) return true;

        var categoryCache = new Dictionary<Guid, Guid?>();
        var eligibleQuantity = 0;

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

            if (promotion.IsLineEligible(line.ProductId, categoryId))
                eligibleQuantity += line.Quantity;
        }

        return eligibleQuantity >= (promotion.MinQuantity ?? 1);
    }

    private static IReadOnlyList<GiftGrant> ToGrants(Promotion promotion) =>
        promotion.GiftItems.Select(i => new GiftGrant(promotion.Id, promotion.Name, i.ProductId, i.Quantity)).ToList();
}
