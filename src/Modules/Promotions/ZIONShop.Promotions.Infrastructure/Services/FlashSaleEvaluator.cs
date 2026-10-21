using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Promotions;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;
using ZIONShop.Promotions.Domain.Repositories;

namespace ZIONShop.Promotions.Infrastructure.Services;

/// <summary>
/// Implements the Contracts-facing flash-sale evaluator. Unlike <see cref="PromotionEvaluator"/>, quota
/// enforcement does not use the xmin optimistic-concurrency retry loop: <see cref="IFlashSaleItemRepository"/>'s
/// TryReserveAsync/ReleaseAsync perform a single atomic conditional UPDATE (bypassing the change tracker),
/// the same pattern already proven in Inventory under concurrency.
/// </summary>
public class FlashSaleEvaluator : IFlashSaleEvaluator
{
    private readonly IFlashSaleRepository _flashSales;
    private readonly IFlashSaleItemRepository _items;
    private readonly IFlashSaleRedemptionRepository _redemptions;
    private readonly IPromotionsUnitOfWork _uow;
    private readonly TimeProvider _timeProvider;

    public FlashSaleEvaluator(
        IFlashSaleRepository flashSales,
        IFlashSaleItemRepository items,
        IFlashSaleRedemptionRepository redemptions,
        IPromotionsUnitOfWork uow,
        TimeProvider timeProvider)
    {
        _flashSales = flashSales;
        _items = items;
        _redemptions = redemptions;
        _uow = uow;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<FlashSaleQuote>> QuoteAsync(IReadOnlyList<FlashSaleLine> lines, CancellationToken ct = default)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var quotes = new List<FlashSaleQuote>();

        foreach (var line in lines)
        {
            var variantKey = line.VariantId ?? line.ProductId;
            var candidates = await FindActiveCandidatesAsync(variantKey, line.Quantity, now, ct);
            var item = candidates.FirstOrDefault();
            if (item is null) continue;

            quotes.Add(new FlashSaleQuote(line.ProductId, item.Id, item.FlashPrice, item.VariantId));
        }

        return quotes;
    }

    public async Task<IReadOnlyList<FlashSaleQuote>> ReserveAsync(IReadOnlyList<FlashSaleLine> lines, Guid orderId, Guid userId, CancellationToken ct = default)
    {
        var existingRedemptions = await _redemptions.GetByOrderIdAsync(orderId, ct);
        var activeByItemId = existingRedemptions
            .Where(r => r.Status != RedemptionStatus.Released)
            .ToDictionary(r => r.FlashSaleItemId);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var quotes = new List<FlashSaleQuote>();
        var changed = false;

        foreach (var line in lines)
        {
            var variantKey = line.VariantId ?? line.ProductId;
            var candidates = await FindActiveCandidatesAsync(variantKey, line.Quantity, now, ct);

            var alreadyRedeemed = candidates.FirstOrDefault(c => activeByItemId.ContainsKey(c.Id));
            if (alreadyRedeemed is not null)
            {
                var existing = activeByItemId[alreadyRedeemed.Id];
                quotes.Add(new FlashSaleQuote(line.ProductId, alreadyRedeemed.Id, existing.UnitFlashPrice, alreadyRedeemed.VariantId));
                continue;
            }

            foreach (var candidate in candidates)
            {
                var reserved = await _items.TryReserveAsync(candidate.Id, line.Quantity, ct);
                if (!reserved) continue;

                var redemption = FlashSaleRedemption.Create(candidate.Id, orderId, userId, line.Quantity, candidate.FlashPrice, now);
                await _redemptions.AddAsync(redemption, ct);
                changed = true;

                quotes.Add(new FlashSaleQuote(line.ProductId, candidate.Id, candidate.FlashPrice, candidate.VariantId));
                break;
            }
        }

        if (changed)
            await _uow.SaveChangesAsync(ct);

        return quotes;
    }

    public async Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        var redemptions = await _redemptions.GetByOrderIdAsync(orderId, ct);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var changed = false;

        foreach (var redemption in redemptions.Where(r => r.Status != RedemptionStatus.Released))
        {
            await _items.ReleaseAsync(redemption.FlashSaleItemId, redemption.Quantity, ct);
            redemption.MarkReleased(now);
            _redemptions.Update(redemption);
            changed = true;
        }

        if (changed)
            await _uow.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Active items for the variant key with enough quota for <paramref name="quantity"/>, cheapest first
    /// (ties broken by the owning campaign's soonest EndsAt). Reserve tries each in order so a quota
    /// shortfall or a lost reservation race on one campaign falls through to the next.
    /// </summary>
    private Task<List<FlashSaleItem>> FindActiveCandidatesAsync(Guid variantId, int quantity, DateTime now, CancellationToken ct)
    {
        var activeFlashSales = _flashSales.Query().AsNoTracking()
            .Where(f => f.IsActive && f.StartsAt <= now && f.EndsAt >= now);

        return _items.Query().AsNoTracking()
            .Where(i => i.VariantId == variantId && i.AllocatedQuantity - i.SoldQuantity >= quantity)
            .Join(activeFlashSales, i => i.FlashSaleId, f => f.Id, (i, f) => new { Item = i, f.EndsAt })
            .OrderBy(x => x.Item.FlashPrice)
            .ThenBy(x => x.EndsAt)
            .Select(x => x.Item)
            .ToListAsync(ct);
    }
}
