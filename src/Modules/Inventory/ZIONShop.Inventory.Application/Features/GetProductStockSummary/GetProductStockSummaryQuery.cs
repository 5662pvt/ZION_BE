using MediatR;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetProductStockSummary;

public record GetProductStockSummaryQuery(IReadOnlyList<Guid> ProductIds) : IRequest<Result<IReadOnlyList<ProductAvailabilityDto>>>;
