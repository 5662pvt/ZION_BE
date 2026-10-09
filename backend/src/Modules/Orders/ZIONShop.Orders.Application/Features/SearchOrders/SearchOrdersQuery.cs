using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Orders.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Orders.Application.Features.SearchOrders;

public record SearchOrdersQuery(
    int Page,
    int PageSize,
    string? Status,
    DateTime? From,
    DateTime? To,
    string? Keyword) : IRequest<Result<PagedResult<OrderSummaryDto>>>;
