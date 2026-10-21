using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetStockMovements;

/// <summary>When <paramref name="IsVariantId"/> is false, <paramref name="Id"/> is resolved via <see cref="Services.ProductOrVariantIdResolver"/> (legacy product-id route); when true it is used directly as a variant id.</summary>
public record GetStockMovementsQuery(Guid Id, bool IsVariantId, int Page, int PageSize) : IRequest<Result<PagedResult<StockMovementDto>>>;
