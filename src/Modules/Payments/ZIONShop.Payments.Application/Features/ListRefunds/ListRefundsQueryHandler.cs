using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.ListRefunds;

public class ListRefundsQueryHandler : IRequestHandler<ListRefundsQuery, Result<PagedResult<RefundDto>>>
{
    private readonly IRefundRepository _refunds;

    public ListRefundsQueryHandler(IRefundRepository refunds) => _refunds = refunds;

    public async Task<Result<PagedResult<RefundDto>>> Handle(ListRefundsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Refund> query = _refunds.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<RefundStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(r => r.Status == status);

        query = query.OrderByDescending(r => r.CreatedDate);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new RefundDto(
                r.Id,
                r.OrderId,
                r.ReturnRequestId,
                r.PaymentId,
                r.Amount,
                r.Currency,
                r.Method,
                r.Status.ToString(),
                r.ProviderRefundId,
                r.FailureReason,
                r.CreatedDate,
                r.CompletedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<RefundDto>.Create(items, page, pageSize, total));
    }
}
