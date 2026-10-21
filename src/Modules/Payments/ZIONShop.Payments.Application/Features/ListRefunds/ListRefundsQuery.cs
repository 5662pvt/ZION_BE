using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Payments.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Payments.Application.Features.ListRefunds;

public record ListRefundsQuery(int Page, int PageSize, string? Status) : IRequest<Result<PagedResult<RefundDto>>>;
