using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.SearchFlashSales;

public class SearchFlashSalesQueryHandler : IRequestHandler<SearchFlashSalesQuery, Result<PagedResult<FlashSaleSummaryDto>>>
{
    private readonly IFlashSaleRepository _flashSales;
    private readonly IFlashSaleItemRepository _items;

    public SearchFlashSalesQueryHandler(IFlashSaleRepository flashSales, IFlashSaleItemRepository items)
    {
        _flashSales = flashSales;
        _items = items;
    }

    public async Task<Result<PagedResult<FlashSaleSummaryDto>>> Handle(SearchFlashSalesQuery request, CancellationToken cancellationToken)
    {
        IQueryable<FlashSale> query = _flashSales.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = EscapeLikePattern(request.Keyword.Trim()).ToUpperInvariant();
            query = query.Where(f => EF.Functions.Like(f.Name.ToUpper(), $"%{kw}%"));
        }

        query = query.OrderByDescending(f => f.StartsAt);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var flashSales = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var ids = flashSales.Select(f => f.Id).ToList();
        var counts = await _items.Query().AsNoTracking()
            .Where(i => ids.Contains(i.FlashSaleId))
            .GroupBy(i => i.FlashSaleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var dtoItems = flashSales
            .Select(f => f.ToSummaryDto(counts.TryGetValue(f.Id, out var count) ? count : 0))
            .ToList();

        return Result.Success(PagedResult<FlashSaleSummaryDto>.Create(dtoItems, page, pageSize, total));
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
