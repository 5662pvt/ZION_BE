using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Tests.Fakes;

internal sealed class FakeLoyaltyMutationService : ILoyaltyMutationService
{
    public List<(Guid UserId, int Points, PointLotSource Source, string SourceRef, Guid? OrderId, Guid? ProductId, string Description)> Earns { get; } = new();
    public List<(Guid OrderId, Guid UserId, string Description)> Revocations { get; } = new();
    public List<(Guid OrderId, string Description)> Refunds { get; } = new();

    public int NextEarnResult { get; set; } = 1;

    public Task<int> EarnAsync(Guid userId, int points, PointLotSource source, string sourceRef, Guid? orderId, Guid? productId, string description, CancellationToken ct = default)
    {
        Earns.Add((userId, points, source, sourceRef, orderId, productId, description));
        return Task.FromResult(NextEarnResult);
    }

    public Task RevokeOrderEarnAsync(Guid orderId, Guid userId, string description, CancellationToken ct = default)
    {
        Revocations.Add((orderId, userId, description));
        return Task.CompletedTask;
    }

    public Task RefundReservationAsync(Guid orderId, string description, CancellationToken ct = default)
    {
        Refunds.Add((orderId, description));
        return Task.CompletedTask;
    }

    public Task<Result> AdminAdjustAsync(Guid userId, int delta, string reason, Guid actorId, CancellationToken ct = default) =>
        Task.FromResult(Result.Success());

    public Task<int> ExpireLotAsync(Guid lotId, CancellationToken ct = default) => Task.FromResult(0);
}
