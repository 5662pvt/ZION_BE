using MediatR;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetAvailabilityBatch;

public record GetAvailabilityBatchQuery(IReadOnlyList<Guid> ProductIds) : IRequest<Result<IReadOnlyList<AvailabilityDto>>>;
