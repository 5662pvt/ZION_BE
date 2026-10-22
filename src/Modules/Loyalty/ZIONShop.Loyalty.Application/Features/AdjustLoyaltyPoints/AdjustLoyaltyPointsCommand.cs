using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Application.Features.AdjustLoyaltyPoints;

public record AdjustLoyaltyPointsCommand(Guid UserId, int Delta, string Reason, Guid ActorId) : IRequest<Result>;
