using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Loyalty.Application.DTOs;
using ZIONShop.Loyalty.Application.Mappings;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Application.Features.GetMyLoyaltyTransactions;

public class GetMyLoyaltyTransactionsQueryHandler : IRequestHandler<GetMyLoyaltyTransactionsQuery, Result<PagedResult<LoyaltyTransactionDto>>>
{
    private readonly IPointTransactionRepository _transactions;

    public GetMyLoyaltyTransactionsQueryHandler(IPointTransactionRepository transactions) => _transactions = transactions;

    public async Task<Result<PagedResult<LoyaltyTransactionDto>>> Handle(GetMyLoyaltyTransactionsQuery request, CancellationToken cancellationToken)
    {
        var query = _transactions.Query().Where(t => t.UserId == request.UserId);

        if (!string.IsNullOrWhiteSpace(request.Type) && Enum.TryParse<PointTransactionType>(request.Type, ignoreCase: true, out var type))
            query = query.Where(t => t.Type == type);

        query = query.OrderByDescending(t => t.CreatedAt);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var paged = await query.ToPagedResultAsync(page, pageSize, cancellationToken);
        var items = paged.Items.Select(t => t.ToDto()).ToList();

        return Result.Success(PagedResult<LoyaltyTransactionDto>.Create(items, paged.Page, paged.PageSize, paged.TotalCount));
    }
}
