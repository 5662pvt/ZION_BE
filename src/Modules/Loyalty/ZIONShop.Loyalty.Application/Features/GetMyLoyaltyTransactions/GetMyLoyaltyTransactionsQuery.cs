using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Loyalty.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Application.Features.GetMyLoyaltyTransactions;

public record GetMyLoyaltyTransactionsQuery(Guid UserId, int Page, int PageSize, string? Type) : IRequest<Result<PagedResult<LoyaltyTransactionDto>>>;
