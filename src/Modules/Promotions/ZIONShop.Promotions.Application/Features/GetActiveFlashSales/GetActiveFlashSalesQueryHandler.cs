using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GetActiveFlashSales;

public class GetActiveFlashSalesQueryHandler : IRequestHandler<GetActiveFlashSalesQuery, Result<IReadOnlyList<ActiveFlashSaleDto>>>
{
    private readonly IFlashSaleRepository _flashSales;
    private readonly IFlashSaleItemRepository _items;
    private readonly IProductPriceLookup _priceLookup;
    private readonly TimeProvider _timeProvider;

    public GetActiveFlashSalesQueryHandler(
        IFlashSaleRepository flashSales,
        IFlashSaleItemRepository items,
        IProductPriceLookup priceLookup,
        TimeProvider timeProvider)
    {
        _flashSales = flashSales;
        _items = items;
        _priceLookup = priceLookup;
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<ActiveFlashSaleDto>>> Handle(GetActiveFlashSalesQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var activeFlashSales = await _flashSales.Query().AsNoTracking()
            .Where(f => f.IsActive && f.StartsAt <= now && f.EndsAt >= now)
            .OrderBy(f => f.EndsAt)
            .ToListAsync(cancellationToken);

        if (activeFlashSales.Count == 0)
            return Result.Success<IReadOnlyList<ActiveFlashSaleDto>>(Array.Empty<ActiveFlashSaleDto>());

        var ids = activeFlashSales.Select(f => f.Id).ToList();
        var items = await _items.Query().AsNoTracking()
            .Where(i => ids.Contains(i.FlashSaleId))
            .ToListAsync(cancellationToken);

        var variantIds = items.Select(i => i.VariantId).Distinct().ToList();
        var productInfo = await _priceLookup.GetManyAsync(variantIds, cancellationToken);

        var result = activeFlashSales.Select(flashSale =>
        {
            var flashItems = items.Where(i => i.FlashSaleId == flashSale.Id).Select(i =>
            {
                productInfo.TryGetValue(i.VariantId, out var info);
                return new ActiveFlashSaleItemDto(
                    i.ProductId,
                    i.VariantId,
                    info?.VariantLabel,
                    info?.Sku,
                    info?.Name ?? string.Empty,
                    info?.PrimaryImageUrl,
                    i.FlashPrice,
                    info?.UnitPrice ?? 0m,
                    i.Available,
                    i.AllocatedQuantity,
                    flashSale.EndsAt);
            }).ToList();

            return new ActiveFlashSaleDto(flashSale.Id, flashSale.Name, flashSale.StartsAt, flashSale.EndsAt, flashItems);
        }).ToList();

        return Result.Success<IReadOnlyList<ActiveFlashSaleDto>>(result);
    }
}
