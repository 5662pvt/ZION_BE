using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.SearchReturns;

public class SearchReturnsQueryHandler : IRequestHandler<SearchReturnsQuery, Result<PagedResult<AdminReturnRequestDto>>>
{
    private readonly IReturnRequestRepository _returns;
    private readonly IOrderRepository _orders;

    public SearchReturnsQueryHandler(IReturnRequestRepository returns, IOrderRepository orders)
    {
        _returns = returns;
        _orders = orders;
    }

    public async Task<Result<PagedResult<AdminReturnRequestDto>>> Handle(SearchReturnsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<ReturnRequest> query = _returns.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ReturnRequestStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(r => r.Status == status);

        query = query.OrderByDescending(r => r.CreatedAt);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_orders.Query().AsNoTracking(), r => r.OrderId, o => o.Id, (r, o) => new AdminReturnRequestDto(
                r.Id,
                r.OrderId,
                o.Code,
                o.ShippingAddress.RecipientName,
                o.ShippingAddress.PhoneNumber,
                r.Reason.ToString(),
                r.Note,
                r.Status.ToString(),
                r.RejectReason,
                r.ReturnTrackingCode,
                r.RefundAmount,
                r.RefundMethod,
                r.CreatedAt,
                r.ApprovedAt,
                r.RejectedAt,
                r.ShippedBackAt,
                r.ReceivedAt,
                r.RefundedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<AdminReturnRequestDto>.Create(items, page, pageSize, total));
    }
}
