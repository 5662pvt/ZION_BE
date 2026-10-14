using MediatR;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetAvailability;

public record GetAvailabilityQuery(Guid ProductId) : IRequest<Result<AvailabilityDto>>;
