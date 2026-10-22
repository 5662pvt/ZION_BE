using MediatR;
using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Application.Features.AdjustLoyaltyPoints;

public class AdjustLoyaltyPointsCommandHandler : IRequestHandler<AdjustLoyaltyPointsCommand, Result>
{
    private readonly ILoyaltyMutationService _mutations;

    public AdjustLoyaltyPointsCommandHandler(ILoyaltyMutationService mutations) => _mutations = mutations;

    public Task<Result> Handle(AdjustLoyaltyPointsCommand request, CancellationToken cancellationToken) =>
        _mutations.AdminAdjustAsync(request.UserId, request.Delta, request.Reason, request.ActorId, cancellationToken);
}
