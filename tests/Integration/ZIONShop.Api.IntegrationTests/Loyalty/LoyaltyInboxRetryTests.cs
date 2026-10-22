using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Loyalty;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;
using ZIONShop.Loyalty.Application.Interfaces;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Infrastructure.Persistence;

namespace ZIONShop.Api.IntegrationTests.Loyalty;

/// <summary>
/// Reproduces the W22 bug: every retry loop in <c>LoyaltyPointsService</c> (Reserve/Release/Earn/Revoke/
/// Refund/AdminAdjust/ExpireLot) used to call <c>ChangeTracker.Clear()</c> on a <c>DbUpdateConcurrencyException</c>,
/// which also detached the <c>InboxMessage</c> row that <c>IdempotentIntegrationEventHandler</c> had already
/// added before invoking the inner handler. A retried delivery then committed its business change but never
/// persisted the inbox row, so the same event could be reprocessed on redelivery. The fix
/// (<c>LoyaltyDbContext.ResetBusinessChanges</c>) detaches everything except the inbox row.
///
/// These tests drive the real <c>IdempotentIntegrationEventHandler</c> decorator and a real Postgres
/// <see cref="LoyaltyDbContext"/> (via <see cref="ApiFactory"/>'s Testcontainers Postgres), with a test-only
/// inner handler that mirrors <c>LoyaltyPointsService</c>'s own retry-on-conflict shape. The concurrency
/// conflict is forced the same way <c>ReleaseCommitConcurrencyTests</c> forces real races elsewhere in this
/// suite: a second, independent <see cref="LoyaltyDbContext"/> commits a change to the same
/// <see cref="PointAccount"/> row (bumping its xmin) between the handler's fetch and its own save -- a real
/// external commit on a separate connection, not a mocked timing hook.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class LoyaltyInboxRetryTests
{
    private readonly ApiFactory _factory;

    public LoyaltyInboxRetryTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Handler_Should_PersistInboxRowOnce_When_InnerHandlerRetriesAfterConcurrencyConflict()
    {
        var userId = Guid.NewGuid();

        // Pre-existing account so the retry loop's save is a concurrency-guarded UPDATE, not a first-time INSERT.
        using (var seedScope = _factory.Services.CreateScope())
        {
            var seedDb = seedScope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();
            seedDb.PointAccounts.Add(PointAccount.Create(userId));
            await seedDb.SaveChangesAsync();
        }

        var state = new RetryState();
        var testFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(state);
            services.AddIntegrationEventHandler<OrderShippedIntegrationEvent, RetryingEarnHandler, LoyaltyDbContext>();
        }));

        var eventBus = testFactory.Services.GetRequiredService<IEventBus>();
        var @event = new OrderShippedIntegrationEvent(Guid.NewGuid(), userId);

        await eventBus.PublishAsync(@event);

        state.ConflictObserved.Should().BeTrue("the test must force a genuine DbUpdateConcurrencyException to exercise the retry path this bug lives in");
        state.EffectCount.Should().Be(1, "the first attempt's conflict rolled back and only the retry committed");

        using (var scope = testFactory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();

            var account = await db.PointAccounts.SingleAsync(a => a.Id == userId);
            account.Balance.Should().Be(11, "the concurrent bump (+1, committed before the retry's fetch) and the handler's own earn (+10, applied once on the retry) both landed");

            var inboxRows = await db.Set<InboxMessage>()
                .Where(m => m.MessageId == @event.EventId && m.Consumer == typeof(RetryingEarnHandler).FullName)
                .CountAsync();
            inboxRows.Should().Be(1, "the inbox row for this delivery must survive the mid-handler retry -- this is the bug being fixed");

            // Scoped to the EventIds this test actually queued (one per attempt) -- the table is shared
            // across the whole test collection/container, so an unscoped count would see other tests' rows.
            var outboxRows = await db.Set<ZIONShop.EventBus.Outbox.OutboxMessage>()
                .CountAsync(m => state.QueuedOutboxEventIds.Contains(m.Id));
            outboxRows.Should().Be(1, "the failed first attempt's queued outbox event must not survive alongside the retry's");
        }

        // Redelivery of the same EventId must now be a no-op.
        await eventBus.PublishAsync(@event);
        state.EffectCount.Should().Be(1);
    }

    private class RetryState
    {
        private int _effectCount;
        public bool ConflictObserved;
        public readonly List<Guid> QueuedOutboxEventIds = new();
        public void RecordEffect() => Interlocked.Increment(ref _effectCount);
        public int EffectCount => _effectCount;
    }

    /// <summary>Mirrors <c>LoyaltyPointsService</c>'s own retry-on-<see cref="DbUpdateConcurrencyException"/>
    /// loop against the real <see cref="PointAccount"/> xmin concurrency token.</summary>
    private class RetryingEarnHandler : IIntegrationEventHandler<OrderShippedIntegrationEvent>
    {
        private readonly LoyaltyDbContext _db;
        private readonly ILoyaltyUnitOfWork _uow;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RetryState _state;

        public RetryingEarnHandler(LoyaltyDbContext db, ILoyaltyUnitOfWork uow, IServiceScopeFactory scopeFactory, RetryState state)
        {
            _db = db;
            _uow = uow;
            _scopeFactory = scopeFactory;
            _state = state;
        }

        public async Task HandleAsync(OrderShippedIntegrationEvent @event, CancellationToken ct = default)
        {
            for (var attempt = 1; attempt <= 2; attempt++)
            {
                var account = await _db.PointAccounts.SingleAsync(a => a.Id == @event.UserId, ct);

                if (attempt == 1)
                {
                    // A genuine external commit on a separate connection/transaction, bumping this row's
                    // xmin after this handler already fetched it -- not a mocked timing hook.
                    using var concurrentScope = _scopeFactory.CreateScope();
                    var concurrentDb = concurrentScope.ServiceProvider.GetRequiredService<LoyaltyDbContext>();
                    var concurrentAccount = await concurrentDb.PointAccounts.SingleAsync(a => a.Id == @event.UserId, ct);
                    concurrentAccount.Increase(1);
                    await concurrentDb.SaveChangesAsync(ct);
                }

                account.Increase(10);
                var earnedEvent = new LoyaltyPointsEarnedIntegrationEvent(@event.UserId, 10, "Order", null, null, DateTime.UtcNow.AddMonths(1));
                _state.QueuedOutboxEventIds.Add(earnedEvent.EventId);
                _uow.AddOutboxEvent(earnedEvent);

                try
                {
                    await _uow.SaveChangesAsync(ct);
                    _state.RecordEffect();
                    return;
                }
                catch (DbUpdateConcurrencyException)
                {
                    _state.ConflictObserved = true;
                    _uow.ResetBusinessChanges();
                }
            }

            throw new InvalidOperationException("Retry budget exhausted in test handler.");
        }
    }
}
