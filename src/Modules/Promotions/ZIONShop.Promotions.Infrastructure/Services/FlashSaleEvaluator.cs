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
            var activeItems = await FindActiveItemsAsync(variantKey, now, ct);
            var item = activeItems.FirstOrDefault(i => i.AllocatedQuantity - i.SoldQuantity >= line.Quantity);
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
            // Checked against EVERY active item for this variant, not just ones with spare quota right now --
            // otherwise a retry whose earlier reservation has since exhausted its campaign's quota (from other
            // orders) would miss its own redemption here and either double-reserve or fall back to a pricier one.
            var activeItems = await FindActiveItemsAsync(variantKey, now, ct);

            var alreadyRedeemed = activeItems.FirstOrDefault(c => activeByItemId.ContainsKey(c.Id));
            if (alreadyRedeemed is not null)
            {
                var existing = activeByItemId[alreadyRedeemed.Id];
                quotes.Add(new FlashSaleQuote(line.ProductId, alreadyRedeemed.Id, existing.UnitFlashPrice, alreadyRedeemed.VariantId));
                continue;
            }

            var candidates = activeItems.Where(i => i.AllocatedQuantity - i.SoldQuantity >= line.Quantity).ToList();
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
    /// Every active item for the variant key, regardless of remaining quota, cheapest first (ties broken by
    /// the owning campaign's soonest EndsAt). Callers that need to attempt a NEW reservation filter this list
    /// by quota themselves; the already-redeemed check must run against the unfiltered list (see ReserveAsync).
    /// </summary>
    private Task<List<FlashSaleItem>> FindActiveItemsAsync(Guid variantId, DateTime now, CancellationToken ct)
    {
        var activeFlashSales = _flashSales.Query().AsNoTracking()
            .Where(f => f.IsActive && f.StartsAt <= now && f.EndsAt >= now);

        return _items.Query().AsNoTracking()
            .Where(i => i.VariantId == variantId)
            .Join(activeFlashSales, i => i.FlashSaleId, f => f.Id, (i, f) => new { Item = i, f.EndsAt })
            .OrderBy(x => x.Item.FlashPrice)
            .ThenBy(x => x.EndsAt)
            .Select(x => x.Item)
            .ToListAsync(ct);
    }
}
