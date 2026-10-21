using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.Inventory.Application.Mappings;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.SearchStock;

public class SearchStockQueryHandler : IRequestHandler<SearchStockQuery, Result<PagedResult<InventoryItemDto>>>
{
    private readonly IInventoryItemRepository _items;

    public SearchStockQueryHandler(IInventoryItemRepository items) => _items = items;

    public async Task<Result<PagedResult<InventoryItemDto>>> Handle(SearchStockQuery request, CancellationToken cancellationToken)
    {
        IQueryable<InventoryItem> query = _items.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = EscapeLikePattern(request.Keyword.Trim()).ToUpperInvariant();
            query = query.Where(i =>
                EF.Functions.Like(i.ProductName.ToUpper(), $"%{kw}%") ||
                EF.Functions.Like(i.Sku.ToUpper(), $"%{kw}%") ||
                (i.VariantLabel != null && EF.Functions.Like(i.VariantLabel.ToUpper(), $"%{kw}%")));
        }

        if (request.LowStockOnly)
            query = query.Where(i => i.OnHand - i.Reserved <= i.LowStockThreshold);

        query = query.OrderBy(i => i.ProductName).ThenBy(i => i.VariantLabel);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var dtos = items.Select(i => i.ToDto()).ToList();
        return Result.Success(PagedResult<InventoryItemDto>.Create(dtos, page, pageSize, total));
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
