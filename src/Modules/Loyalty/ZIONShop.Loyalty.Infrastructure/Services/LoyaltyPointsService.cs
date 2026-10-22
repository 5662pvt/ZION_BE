using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ZIONShop.Contracts.Loyalty;
using ZIONShop.Contracts.Orders;
using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.Loyalty.Application.Options;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;
using ZIONShop.Loyalty.Domain.Exceptions;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Infrastructure.Services;

/// <summary>
/// Implements both the Contracts-facing <see cref="ILoyaltyPointsService"/> (Reserve/Release/Quote/Balance,
/// called by Orders) and the module-internal <see cref="ILoyaltyMutationService"/> (Earn/Revoke/Refund/
/// AdminAdjust/Expire, called by Loyalty's own integration-event handlers and admin command) so every mutation
/// of <see cref="PointAccount.Balance"/> shares one retry loop: on a PostgreSQL xmin conflict the whole change
/// tracker is cleared and the attempt re-fetches fresh state instead of re-applying mutations on stale entities.
/// </summary>
public class LoyaltyPointsService : ILoyaltyPointsService, ILoyaltyMutationService
{
    private const int MaxAttempts = 3;
    private static readonly TimeSpan GiveBackToSameLotThreshold = TimeSpan.FromDays(1);

    private readonly IPointAccountRepository _accounts;
    private readonly IPointLotRepository _lots;
    private readonly IPointTransactionRepository _transactions;
    private readonly IPointReservationRepository _reservations;
    private readonly IOrderPointsRevocationRepository _revocations;
    private readonly ILoyaltyUnitOfWork _uow;
    private readonly IOptions<LoyaltyOptions> _options;
    private readonly IOrderDetailsReader _orderDetailsReader;
    private readonly TimeProvider _timeProvider;

    public LoyaltyPointsService(
        IPointAccountRepository accounts,
        IPointLotRepository lots,
        IPointTransactionRepository transactions,
        IPointReservationRepository reservations,
        IOrderPointsRevocationRepository revocations,
        ILoyaltyUnitOfWork uow,
        IOptions<LoyaltyOptions> options,
        IOrderDetailsReader orderDetailsReader,
        TimeProvider timeProvider)
    {
        _accounts = accounts;
        _lots = lots;
        _transactions = transactions;
        _reservations = reservations;
        _revocations = revocations;
        _uow = uow;
        _options = options;
        _orderDetailsReader = orderDetailsReader;
        _timeProvider = timeProvider;
    }

    public async Task<int> GetBalanceAsync(Guid userId, CancellationToken ct = default)
    {
        var account = await _accounts.GetByUserIdAsync(userId, ct);
        return account?.Balance ?? 0;
    }

    public async Task<int> QuoteAsync(Guid userId, decimal eligibleAmount, CancellationToken ct = default)
    {
        var balance = await GetBalanceAsync(userId, ct);
        return ComputeUsable(balance, eligibleAmount);
    }

    public async Task<(LoyaltyRedemption? Redemption, LoyaltyError? Error)> ReserveAsync(Guid userId, Guid orderId, decimal eligibleAmount, CancellationToken ct = default)
    {
        var existing = await _reservations.GetByOrderIdAsync(orderId, ct);
        if (existing is not null)
        {
            // A fresh reservation for an orderId that already released is not supported (PointReservation.OrderId
            // is unique) -- not exercised by Orders in practice, since a released order never re-checks-out with
            // the same orderId.
            return existing.Status != PointReservationStatus.Released
                ? (new LoyaltyRedemption(orderId, existing.Points, existing.DiscountAmount), null)
                : (null, null);
        }

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var account = await _accounts.GetByUserIdAsync(userId, ct);
            var points = ComputeUsable(account?.Balance ?? 0, eligibleAmount);
            if (points <= 0) return (null, null);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var activeLots = await _lots.GetActiveForUserAsync(userId, now, ct);
            var (allocations, allocated) = AllocateFifo(activeLots, points);
            if (allocated <= 0) return (null, null);

            account!.Decrease(allocated);
            _accounts.Update(account);
            foreach (var lotId in allocations.Select(a => a.LotId))
                _lots.Update(activeLots.First(l => l.Id == lotId));

            var reservation = PointReservation.Create(orderId, userId, allocated, allocated, allocations, now);
            await _reservations.AddAsync(reservation, ct);

            var code = await GetOrderCodeAsync(orderId, ct);
            var transaction = PointTransaction.Create(userId, PointTransactionType.Spend, -allocated, account.Balance, orderId, $"Dùng Xu cho đơn #{code}", now);
            await _transactions.AddAsync(transaction, ct);

            try
            {
                await _uow.SaveChangesAsync(ct);
                return (new LoyaltyRedemption(orderId, allocated, allocated), null);
            }
            catch (DbUpdateConcurrencyException)
            {
                _uow.ClearChangeTracker();
            }
        }

        return (null, new LoyaltyError("Loyalty.ReserveConflict", "Không thể dùng Xu lúc này, vui lòng thử lại sau"));
    }

    public async Task ReleaseAsync(Guid orderId, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var reservation = await _reservations.GetByOrderIdAsync(orderId, ct);
            if (reservation is null || reservation.Status != PointReservationStatus.Reserved) return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            await GiveBackAllocationsAsync(reservation, now, ct);

            reservation.MarkReleased(now);
            _reservations.Update(reservation);

            var (account, isNew) = await GetOrCreateAccountAsync(reservation.UserId, ct);
            account.Increase(reservation.Points);
            if (isNew) await _accounts.AddAsync(account, ct); else _accounts.Update(account);

            var code = await GetOrderCodeAsync(orderId, ct);
            var transaction = PointTransaction.Create(reservation.UserId, PointTransactionType.Release, reservation.Points, account.Balance, orderId, $"Hoàn Xu do huỷ đơn #{code}", now);
            await _transactions.AddAsync(transaction, ct);
            _uow.AddOutboxEvent(new LoyaltyPointsReturnedIntegrationEvent(reservation.UserId, reservation.Points, orderId, "OrderCancelled"));

            try
            {
                await _uow.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                _uow.ClearChangeTracker();
            }
        }
    }

    public async Task<int> EarnAsync(Guid userId, int points, PointLotSource source, string sourceRef, Guid? orderId, Guid? productId, string description, CancellationToken ct = default)
    {
        if (points <= 0) return 0;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            if (source == PointLotSource.Order && orderId.HasValue && await _revocations.ExistsAsync(orderId.Value, ct))
                return 0; // this order's points were already revoked by a refund that beat this (late) earn

            if (await _lots.GetByUserSourceRefAsync(userId, source, sourceRef, ct) is not null)
                return 0; // idempotent: already earned

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var expiresAt = now.AddMonths(_options.Value.LotLifetimeMonths);
            await _lots.AddAsync(PointLot.Create(userId, source, sourceRef, points, now, expiresAt), ct);

            var (account, isNew) = await GetOrCreateAccountAsync(userId, ct);
            account.Increase(points);
            if (isNew) await _accounts.AddAsync(account, ct); else _accounts.Update(account);

            var transaction = PointTransaction.Create(userId, PointTransactionType.Earn, points, account.Balance, orderId, description, now);
            await _transactions.AddAsync(transaction, ct);
            _uow.AddOutboxEvent(new LoyaltyPointsEarnedIntegrationEvent(userId, points, source.ToString(), orderId, productId, expiresAt));

            try
            {
                await _uow.SaveChangesAsync(ct);
                return points;
            }
            catch (DbUpdateConcurrencyException)
            {
                _uow.ClearChangeTracker();
            }
            catch (DbUpdateException)
            {
                // Unique-index race on (UserId, Source, SourceRef): a concurrent delivery already earned this.
                _uow.ClearChangeTracker();
                return 0;
            }
        }

        return 0;
    }

    public async Task RevokeOrderEarnAsync(Guid orderId, Guid userId, string description, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var markerExisted = await _revocations.ExistsAsync(orderId, ct);
            if (!markerExisted)
                await _revocations.AddAsync(OrderPointsRevocation.Create(orderId, userId, now), ct);

            var lot = await _lots.GetByUserSourceRefAsync(userId, PointLotSource.Order, orderId.ToString(), ct);
            var removed = 0;
            if (lot is not null && lot.Remaining > 0)
            {
                removed = lot.ZeroOut();
                _lots.Update(lot);

                var (account, isNew) = await GetOrCreateAccountAsync(userId, ct);
                account.Decrease(removed);
                if (isNew) await _accounts.AddAsync(account, ct); else _accounts.Update(account);

                await _transactions.AddAsync(PointTransaction.Create(userId, PointTransactionType.Revoke, -removed, account.Balance, orderId, description, now), ct);
            }

            if (markerExisted && removed == 0) return; // already revoked previously, nothing new to persist

            try
            {
                await _uow.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                _uow.ClearChangeTracker();
            }
            catch (DbUpdateException)
            {
                // Marker unique-index race: a concurrent revoke beat us to it.
                _uow.ClearChangeTracker();
                return;
            }
        }
    }

    public async Task RefundReservationAsync(Guid orderId, string description, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var reservation = await _reservations.GetByOrderIdAsync(orderId, ct);
            if (reservation is null || reservation.Status != PointReservationStatus.Committed) return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            await GiveBackAllocationsAsync(reservation, now, ct);

            reservation.MarkRefunded(now);
            _reservations.Update(reservation);

            var (account, isNew) = await GetOrCreateAccountAsync(reservation.UserId, ct);
            account.Increase(reservation.Points);
            if (isNew) await _accounts.AddAsync(account, ct); else _accounts.Update(account);

            await _transactions.AddAsync(PointTransaction.Create(reservation.UserId, PointTransactionType.Refund, reservation.Points, account.Balance, orderId, description, now), ct);
            _uow.AddOutboxEvent(new LoyaltyPointsReturnedIntegrationEvent(reservation.UserId, reservation.Points, orderId, "OrderRefunded"));

            try
            {
                await _uow.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException)
            {
                _uow.ClearChangeTracker();
            }
        }
    }

    public async Task<Result> AdminAdjustAsync(Guid userId, int delta, string reason, Guid actorId, CancellationToken ct = default)
    {
        if (delta == 0) return Result.Failure(LoyaltyErrors.InvalidAdjustment);
        if (string.IsNullOrWhiteSpace(reason)) return Result.Failure(LoyaltyErrors.ReasonRequired);

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var (account, isNew) = await GetOrCreateAccountAsync(userId, ct);
            var now = _timeProvider.GetUtcNow().UtcDateTime;

            if (delta > 0)
            {
                var expiresAt = now.AddMonths(_options.Value.LotLifetimeMonths);
                await _lots.AddAsync(PointLot.Create(userId, PointLotSource.AdminAdjust, Guid.NewGuid().ToString("N"), delta, now, expiresAt), ct);
                account.Increase(delta);
                _uow.AddOutboxEvent(new LoyaltyPointsEarnedIntegrationEvent(userId, delta, "AdminAdjust", null, null, expiresAt));
            }
            else
            {
                var amount = -delta;
                if (account.Balance < amount) return Result.Failure(LoyaltyErrors.InsufficientPoints);

                var activeLots = await _lots.GetActiveForUserAsync(userId, now, ct);
                var (allocations, deducted) = AllocateFifo(activeLots, amount);
                if (deducted < amount) return Result.Failure(LoyaltyErrors.InsufficientPoints);

                foreach (var lotId in allocations.Select(a => a.LotId))
                    _lots.Update(activeLots.First(l => l.Id == lotId));
                account.Decrease(deducted);
            }

            if (isNew) await _accounts.AddAsync(account, ct); else _accounts.Update(account);
            await _transactions.AddAsync(PointTransaction.Create(userId, PointTransactionType.AdminAdjust, delta, account.Balance, null, $"Điều chỉnh bởi quản trị: {reason}", now, actorId), ct);

            try
            {
                await _uow.SaveChangesAsync(ct);
                return Result.Success();
            }
            catch (DbUpdateConcurrencyException)
            {
                _uow.ClearChangeTracker();
            }
        }

        return Result.Failure(Error.Failure("Loyalty.AdjustConflict", "Không thể điều chỉnh lúc này, vui lòng thử lại"));
    }

    public async Task<int> ExpireLotAsync(Guid lotId, CancellationToken ct = default)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            var lot = await _lots.GetByIdAsync(lotId, ct);
            if (lot is null || lot.Remaining <= 0) return 0;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var removed = lot.ZeroOut();
            _lots.Update(lot);

            var (account, isNew) = await GetOrCreateAccountAsync(lot.UserId, ct);
            account.Decrease(removed);
            if (isNew) await _accounts.AddAsync(account, ct); else _accounts.Update(account);

            await _transactions.AddAsync(PointTransaction.Create(lot.UserId, PointTransactionType.Expire, -removed, account.Balance, null, "Xu hết hạn", now), ct);

            try
            {
                await _uow.SaveChangesAsync(ct);
                return removed;
            }
            catch (DbUpdateConcurrencyException)
            {
                _uow.ClearChangeTracker();
            }
        }

        return 0;
    }

    private async Task GiveBackAllocationsAsync(PointReservation reservation, DateTime now, CancellationToken ct)
    {
        foreach (var allocation in reservation.Allocations)
        {
            var lot = await _lots.GetByIdAsync(allocation.LotId, ct);
            if (lot is not null && lot.ExpiresAt > now + GiveBackToSameLotThreshold)
            {
                lot.AddBack(allocation.Points);
                _lots.Update(lot);
            }
            else
            {
                var restoreLot = PointLot.Create(reservation.UserId, PointLotSource.Restore, Guid.NewGuid().ToString("N"), allocation.Points, now, now.AddDays(_options.Value.RestoreLotDays));
                await _lots.AddAsync(restoreLot, ct);
            }
        }
    }

    private int ComputeUsable(int balance, decimal eligibleAmount)
    {
        if (balance <= 0 || eligibleAmount <= 0) return 0;
        var cap = (int)Math.Floor(eligibleAmount * _options.Value.MaxRedeemPercent / 100m);
        return Math.Max(0, Math.Min(balance, cap));
    }

    private static (List<(Guid LotId, int Points)> Allocations, int Allocated) AllocateFifo(List<PointLot> lots, int amount)
    {
        var allocations = new List<(Guid LotId, int Points)>();
        var remaining = amount;
        foreach (var lot in lots)
        {
            if (remaining <= 0) break;
            var taken = lot.Consume(remaining);
            if (taken <= 0) continue;
            allocations.Add((lot.Id, taken));
            remaining -= taken;
        }

        return (allocations, amount - remaining);
    }

    private async Task<(PointAccount Account, bool IsNew)> GetOrCreateAccountAsync(Guid userId, CancellationToken ct)
    {
        var account = await _accounts.GetByUserIdAsync(userId, ct);
        return account is null ? (PointAccount.Create(userId), true) : (account, false);
    }

    private async Task<string> GetOrderCodeAsync(Guid orderId, CancellationToken ct)
    {
        var order = await _orderDetailsReader.GetAsync(orderId, ct);
        return order?.Code ?? orderId.ToString("N")[..8];
    }
}
