using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetStockMovements;

public record GetStockMovementsQuery(Guid ProductId, int Page, int PageSize) : IRequest<Result<PagedResult<StockMovementDto>>>;
