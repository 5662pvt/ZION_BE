using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.GetMyOrders;

public record GetMyOrdersQuery(Guid UserId, int Page, int PageSize, string? Status) : IRequest<Result<PagedResult<OrderSummaryDto>>>;
