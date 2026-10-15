using System.Globalization;
using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Application.Mappings;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Exceptions;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.GetMyOrders;

public class GetMyOrdersQueryHandler : IRequestHandler<GetMyOrdersQuery, Result<PagedResult<OrderSummaryDto>>>
{
    private const string VnDateFormat = "yyyy-MM-dd";
    private static readonly TimeSpan VnOffset = TimeSpan.FromHours(7);

    private readonly IOrderRepository _orders;

    public GetMyOrdersQueryHandler(IOrderRepository orders) => _orders = orders;

    // from/to format, ordering and max-span checks are enforced here rather than via FluentValidation:
    // ValidationBehavior always prefixes a FluentValidation failure code with the property name, which
    // cannot produce the contract-exact "Orders.InvalidDateRange" code (same reasoning as PlaceOrderCommandValidator).
    public async Task<Result<PagedResult<OrderSummaryDto>>> Handle(GetMyOrdersQuery request, CancellationToken cancellationToken)
    {
        DateOnly? from = null;
        if (!string.IsNullOrWhiteSpace(request.From))
        {
            if (!DateOnly.TryParseExact(request.From, VnDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom))
                return Result.Failure<PagedResult<OrderSummaryDto>>(OrdersErrors.InvalidDateRange);
            from = parsedFrom;
        }

        DateOnly? to = null;
        if (!string.IsNullOrWhiteSpace(request.To))
        {
            if (!DateOnly.TryParseExact(request.To, VnDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo))
                return Result.Failure<PagedResult<OrderSummaryDto>>(OrdersErrors.InvalidDateRange);
            to = parsedTo;
        }

        if (from.HasValue && to.HasValue && (from.Value > to.Value || to.Value.DayNumber - from.Value.DayNumber > 366))
            return Result.Failure<PagedResult<OrderSummaryDto>>(OrdersErrors.InvalidDateRange);

        IQueryable<Order> query = _orders.Query().AsNoTracking().Where(o => o.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<OrderStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(o => o.Status == status);

        if (from.HasValue)
        {
            // CreatedDate is "timestamp with time zone" in Postgres: Npgsql requires DateTimeKind.Utc,
            // not the Unspecified kind that DateOnly.ToDateTime(...) - TimeSpan would produce.
            var utcFrom = new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), VnOffset).UtcDateTime;
            query = query.Where(o => o.CreatedDate >= utcFrom);
        }

        if (to.HasValue)
        {
            var utcToExclusive = new DateTimeOffset(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue), VnOffset).UtcDateTime;
            query = query.Where(o => o.CreatedDate < utcToExclusive);
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
}
