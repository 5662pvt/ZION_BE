using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Application.Interfaces;

/// <summary>
/// Internal point-balance mutation surface (earn/revoke/refund/admin-adjust/expire), implemented in
/// Infrastructure alongside <see cref="ZIONShop.Contracts.Loyalty.ILoyaltyPointsService"/> (which only exposes
/// Reserve/Release/Quote/Balance to other modules) so every mutation of <c>PointAccount.Balance</c> shares the
/// same xmin-guarded retry loop. Used only by Loyalty's own integration-event handlers and admin command.
/// </summary>
public interface ILoyaltyMutationService
{
    /// <summary>Creates an idempotent earn lot (skips if (UserId, Source, SourceRef) already exists, if a
    /// revocation marker already exists for an Order-sourced earn, or if points &lt;= 0) and publishes
    /// LoyaltyPointsEarnedIntegrationEvent. Returns the points actually earned (0 if skipped).</summary>
    Task<int> EarnAsync(Guid userId, int points, PointLotSource source, string sourceRef, Guid? orderId, Guid? productId, string description, CancellationToken ct = default);

    /// <summary>Revokes points earned on orderId (clamped per-lot, never touches other lots) and records a
    /// revocation marker so a late earn for the same order is skipped. Idempotent.</summary>
    Task RevokeOrderEarnAsync(Guid orderId, Guid userId, string description, CancellationToken ct = default);

    /// <summary>Gives back a Committed reservation's spent points (order return/refund), marking it Refunded.
    /// Idempotent; no-op if no Committed reservation exists for orderId.</summary>
    Task RefundReservationAsync(Guid orderId, string description, CancellationToken ct = default);

    /// <summary>delta &gt; 0 creates an AdminAdjust lot; delta &lt; 0 deducts FIFO, failing with
    /// Loyalty.InsufficientPoints if the balance is too low.</summary>
    Task<Result> AdminAdjustAsync(Guid userId, int delta, string reason, Guid actorId, CancellationToken ct = default);

    /// <summary>Zeroes out whatever remains of an expired lot and writes an Expire ledger entry. Returns the
    /// number of points removed (0 if the lot had nothing left).</summary>
    Task<int> ExpireLotAsync(Guid lotId, CancellationToken ct = default);
}
