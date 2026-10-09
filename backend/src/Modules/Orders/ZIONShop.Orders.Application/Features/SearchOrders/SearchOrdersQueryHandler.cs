using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.SearchOrders;

public class SearchOrdersQueryHandler : IRequestHandler<SearchOrdersQuery, Result<PagedResult<OrderSummaryDto>>>
{
    private readonly IOrderRepository _orders;

    public SearchOrdersQueryHandler(IOrderRepository orders) => _orders = orders;

    public async Task<Result<PagedResult<OrderSummaryDto>>> Handle(SearchOrdersQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Order> query = _orders.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(o => o.Status == status);

        if (request.From.HasValue)
            query = query.Where(o => o.CreatedDate >= request.From.Value);

        if (request.To.HasValue)
            query = query.Where(o => o.CreatedDate <= request.To.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = EscapeLikePattern(request.Keyword.Trim()).ToUpperInvariant();
            query = query.Where(o => EF.Functions.Like(o.Code.ToUpper(), $"%{kw}%"));
        }

        query = query.OrderByDescending(o => o.CreatedDate);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryDto(o.Id, o.Code, o.Status.ToString(), o.Total, o.Currency, o.CreatedDate, o.Lines.Count))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<OrderSummaryDto>.Create(items, page, pageSize, total));
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
