using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;
using ZIONShop.Payments.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.ListPayments;

public class ListPaymentsQueryHandler : IRequestHandler<ListPaymentsQuery, Result<PagedResult<AdminPaymentSummaryDto>>>
{
    private readonly IPaymentRepository _payments;

    public ListPaymentsQueryHandler(IPaymentRepository payments) => _payments = payments;

    public async Task<Result<PagedResult<AdminPaymentSummaryDto>>> Handle(ListPaymentsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<Payment> query = _payments.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<PaymentStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(p => p.Status == status);

        if (!string.IsNullOrWhiteSpace(request.Channel))
            query = query.Where(p => p.Channel.ToUpper() == request.Channel.Trim().ToUpperInvariant());

        if (request.From.HasValue)
            query = query.Where(p => p.CreatedDate >= request.From.Value.Date);

        if (request.To.HasValue)
            query = query.Where(p => p.CreatedDate < request.To.Value.Date.AddDays(1));

        if (request.OrderId.HasValue)
            query = query.Where(p => p.OrderId == request.OrderId.Value);

        query = query.OrderByDescending(p => p.CreatedDate);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new AdminPaymentSummaryDto(
                p.Id,
                p.OrderId,
                p.UserId,
                p.Amount,
                p.Currency,
                p.Provider,
                p.Channel,
                p.Status.ToString(),
                p.FailureReason,
                p.CreatedDate,
                p.CompletedAt,
                p.ExpiresAt))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<AdminPaymentSummaryDto>.Create(items, page, pageSize, total));
    }
}
