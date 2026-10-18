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
            var item = await FindActiveItemAsync(line.ProductId, now, ct);
            if (item is null || item.Available < line.Quantity) continue;

            quotes.Add(new FlashSaleQuote(line.ProductId, item.Id, item.FlashPrice));
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
            var item = await FindActiveItemAsync(line.ProductId, now, ct);
            if (item is null) continue;

            if (activeByItemId.TryGetValue(item.Id, out var existing))
            {
                quotes.Add(new FlashSaleQuote(line.ProductId, item.Id, existing.UnitFlashPrice));
                continue;
            }

            var reserved = await _items.TryReserveAsync(item.Id, line.Quantity, ct);
            if (!reserved) continue;

            var redemption = FlashSaleRedemption.Create(item.Id, orderId, userId, line.Quantity, item.FlashPrice, now);
            await _redemptions.AddAsync(redemption, ct);
            changed = true;

            quotes.Add(new FlashSaleQuote(line.ProductId, item.Id, item.FlashPrice));
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

    private Task<FlashSaleItem?> FindActiveItemAsync(Guid productId, DateTime now, CancellationToken ct)
    {
        var activeFlashSaleIds = _flashSales.Query().AsNoTracking()
            .Where(f => f.IsActive && f.StartsAt <= now && f.EndsAt >= now)
            .Select(f => f.Id);

        return _items.Query().AsNoTracking()
            .Where(i => i.ProductId == productId && activeFlashSaleIds.Contains(i.FlashSaleId))
            .OrderBy(i => i.FlashPrice)
            .FirstOrDefaultAsync(ct);
    }
}
