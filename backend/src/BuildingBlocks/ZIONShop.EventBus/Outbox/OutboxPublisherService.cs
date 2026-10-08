using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.EventBus.Outbox;

public class OutboxPublisherService<TDbContext> : BackgroundService where TDbContext : DbContext
{
    private const int BatchSize = 50;
    private const int MaxAttempts = 10;

    private static readonly ConcurrentDictionary<Type, Func<IEventBus, IntegrationEvent, CancellationToken, Task>> Dispatchers = new();

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisherService<TDbContext>> _logger;
    private readonly TimeSpan _interval;

    public OutboxPublisherService(
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<OutboxPublisherService<TDbContext>> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.OutboxPollSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox publisher batch failed for {DbContext}", typeof(TDbContext).Name);
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Shutting down.
            }
        }
    }

    public async Task PublishBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TDbContext>();
        var eventBus = scope.ServiceProvider.GetRequiredService<IEventBus>();

        var messages = await db.Set<OutboxMessage>()
            .Where(m => m.ProcessedOn == null && m.Attempts < MaxAttempts)
            .OrderBy(m => m.OccurredOn)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"Unknown outbox event type '{message.Type}'.");
                var @event = (IntegrationEvent)(JsonSerializer.Deserialize(message.Content, eventType)
                    ?? throw new InvalidOperationException($"Outbox message '{message.Id}' deserialized to null."));

                var dispatch = Dispatchers.GetOrAdd(eventType, BuildDispatcher);
                await dispatch(eventBus, @event, cancellationToken);

                message.ProcessedOn = DateTime.UtcNow;
                message.Error = null;
            }
            catch (Exception ex)
            {
                message.Attempts++;
                message.Error = ex.Message;
                _logger.LogError(ex, "Failed to publish outbox message {MessageId} ({MessageType}), attempt {Attempts}",
                    message.Id, message.Type, message.Attempts);
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static Func<IEventBus, IntegrationEvent, CancellationToken, Task> BuildDispatcher(Type eventType)
    {
        var busParam = Expression.Parameter(typeof(IEventBus), "bus");
        var eventParam = Expression.Parameter(typeof(IntegrationEvent), "evt");
        var ctParam = Expression.Parameter(typeof(CancellationToken), "ct");

        var publishMethod = typeof(IEventBus).GetMethod(nameof(IEventBus.PublishAsync))!.MakeGenericMethod(eventType);
        var call = Expression.Call(busParam, publishMethod, Expression.Convert(eventParam, eventType), ctParam);

        return Expression.Lambda<Func<IEventBus, IntegrationEvent, CancellationToken, Task>>(call, busParam, eventParam, ctParam).Compile();
    }
}
