using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.SearchReturns;

public record SearchReturnsQuery(int Page, int PageSize, string? Status) : IRequest<Result<PagedResult<AdminReturnRequestDto>>>;
