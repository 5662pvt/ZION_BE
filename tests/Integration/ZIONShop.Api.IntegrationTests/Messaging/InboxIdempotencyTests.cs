using System.Collections.Concurrent;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.DependencyInjection;
using ZIONShop.EventBus.Inbox;
using ZIONShop.Notifications.Infrastructure.Persistence;
using static ZIONShop.Api.IntegrationTests.Notifications.NotificationsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Messaging;

[Collection(ApiTestCollection.Name)]
public class InboxIdempotencyTests
{
    private readonly ApiFactory _factory;

    public InboxIdempotencyTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Handler_Should_RunExactlyOnce_When_SameEventIdDeliveredTenTimes()
    {
        var counter = new CallCounter();
        var testFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(counter);
            services.AddIntegrationEventHandler<OrderShippedIntegrationEvent, CountingHandler, NotificationsDbContext>();
        }));

        var eventBus = testFactory.Services.GetRequiredService<IEventBus>();
        var @event = new OrderShippedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid());

        for (var i = 0; i < 10; i++)
            await eventBus.PublishAsync(@event);

        counter.Count.Should().Be(1);

        using var scope = testFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var rows = await db.Set<InboxMessage>()
            .Where(m => m.MessageId == @event.EventId && m.Consumer == typeof(CountingHandler).FullName)
            .CountAsync();
        rows.Should().Be(1);
    }

    [Fact]
    public async Task Handler_Should_RecordEffectOnlyOnSuccessfulAttempt_When_FirstDeliveryThrows()
    {
        var state = new FlakyState();
        var testFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(state);
            services.AddIntegrationEventHandler<OrderShippedIntegrationEvent, FlakyHandler, NotificationsDbContext>();
        }));

        var eventBus = testFactory.Services.GetRequiredService<IEventBus>();
        var @event = new OrderShippedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid());

        // InMemoryEventBus catches and logs handler exceptions per-handler, so the first (throwing)
        // delivery completes without surfacing the exception to the caller.
        await eventBus.PublishAsync(@event);
        await eventBus.PublishAsync(@event);

        state.EffectCount.Should().Be(1);

        using var scope = testFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
        var rows = await db.Set<InboxMessage>()
            .Where(m => m.MessageId == @event.EventId && m.Consumer == typeof(FlakyHandler).FullName)
            .CountAsync();
        rows.Should().Be(1);
    }

    [Fact]
    public async Task RealConsumer_Should_ApplyEffectOnce_When_SameEventDeliveredThreeTimes()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var eventBus = _factory.Services.GetRequiredService<IEventBus>();
        // Unknown order/user -> the real Notifications.OrderCancelledIntegrationEventHandler still records
        // exactly one NotificationLog("Skipped") row per EventId, giving a countable real-consumer effect
        // without needing a full checkout flow.
        var @event = new OrderCancelledIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), "test reason");

        await eventBus.PublishAsync(@event);
        await eventBus.PublishAsync(@event);
        await eventBus.PublishAsync(@event);

        var logs = await GetNotificationLogsAsync(admin, @event.OrderId);
        logs.Count(l => l.Type == "OrderCancelled").Should().Be(1);
    }

    private class CallCounter
    {
        private int _count;
        public void Increment() => Interlocked.Increment(ref _count);
        public int Count => _count;
    }

    private class CountingHandler : IIntegrationEventHandler<OrderShippedIntegrationEvent>
    {
        private readonly CallCounter _counter;
        public CountingHandler(CallCounter counter) => _counter = counter;

        public Task HandleAsync(OrderShippedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            _counter.Increment();
            return Task.CompletedTask;
        }
    }

    private class FlakyState
    {
        private readonly ConcurrentDictionary<Guid, int> _attempts = new();
        private int _effectCount;

        public int NextAttempt(Guid eventId) => _attempts.AddOrUpdate(eventId, 1, (_, v) => v + 1);
        public void RecordEffect() => Interlocked.Increment(ref _effectCount);
        public int EffectCount => _effectCount;
    }

    private class FlakyHandler : IIntegrationEventHandler<OrderShippedIntegrationEvent>
    {
        private readonly FlakyState _state;
        public FlakyHandler(FlakyState state) => _state = state;

        public Task HandleAsync(OrderShippedIntegrationEvent @event, CancellationToken cancellationToken = default)
        {
            var attempt = _state.NextAttempt(@event.EventId);
            if (attempt == 1)
                throw new InvalidOperationException("Simulated transient failure on first delivery");

            _state.RecordEffect();
            return Task.CompletedTask;
        }
    }
}
