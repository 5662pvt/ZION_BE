using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Diagnostics;
using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.EventBus.Outbox;

public class OutboxPublisherService<TDbContext> : BackgroundService where TDbContext : DbContext
{
    private const int BatchSize = 50;

    /// <summary>
    /// Rows with <c>Attempts >= MaxAttempts</c> and <c>ProcessedOn == null</c> are considered
    /// failed (an admin screen lists them -- see roadmap W14); public so other layers can query
    /// the same threshold instead of re-guessing it.
    /// </summary>
    public const int MaxAttempts = 10;

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

        await UpdateOutboxGaugesAsync(db, cancellationToken);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            Activity? activity = null;
            try
            {
                var eventType = Type.GetType(message.Type)
                    ?? throw new InvalidOperationException($"Unknown outbox event type '{message.Type}'.");
                var @event = (IntegrationEvent)(JsonSerializer.Deserialize(message.Content, eventType)
                    ?? throw new InvalidOperationException($"Outbox message '{message.Id}' deserialized to null."));

                var parentContext = MessagingDiagnostics.TryParseTraceParent(message.TraceParent, message.TraceState, out var context)
                    ? context
                    : default;
                activity = MessagingDiagnostics.ActivitySource.StartActivity(
                    $"{eventType.Name} publish", ActivityKind.Producer, parentContext);
                activity?.SetTag("event.type", eventType.Name);
                activity?.SetTag("messaging.message.id", message.Id.ToString());
                activity?.SetTag("messaging.operation", "publish");

                using var logScope = IntegrationEventLogScope.Begin(_logger, message.Id, eventType.Name, @event);

                var dispatch = Dispatchers.GetOrAdd(eventType, BuildDispatcher);
                await dispatch(eventBus, @event, cancellationToken);

                MessagingDiagnostics.Published.Add(1, new KeyValuePair<string, object?>("event.type", eventType.Name));

                message.ProcessedOn = DateTime.UtcNow;
                message.Error = null;
            }
            catch (BrokerUnavailableException ex)
            {
                MessagingDiagnostics.RecordException(activity, ex);

                // The broker is down, not this particular message's fault -- stop the batch
                // immediately without penalizing this or any remaining message's Attempts, so an
                // outage longer than MaxAttempts polls can never silently abandon an event.
                _logger.LogWarning(ex,
                    "RabbitMQ broker unavailable; stopping outbox batch for {DbContext} early ({Processed}/{Total} messages processed this batch)",
                    typeof(TDbContext).Name, messages.IndexOf(message), messages.Count);
                break;
            }
            catch (Exception ex)
            {
                MessagingDiagnostics.RecordException(activity, ex);

                message.Attempts++;
                message.Error = ex.Message;
                _logger.LogError(ex, "Failed to publish outbox message {MessageId} ({MessageType}), attempt {Attempts}",
                    message.Id, message.Type, message.Attempts);

                if (message.Attempts >= MaxAttempts)
                {
                    _logger.LogCritical(
                        "Outbox message {MessageId} ({MessageType}) moved to failed after {Attempts} attempts",
                        message.Id, message.Type, message.Attempts);
                }
            }
            finally
            {
                activity?.Dispose();
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task UpdateOutboxGaugesAsync(TDbContext db, CancellationToken cancellationToken)
    {
        var module = typeof(TDbContext).Name;
        var pending = await db.Set<OutboxMessage>()
            .CountAsync(m => m.ProcessedOn == null && m.Attempts < MaxAttempts, cancellationToken);
        var failed = await db.Set<OutboxMessage>()
            .CountAsync(m => m.ProcessedOn == null && m.Attempts >= MaxAttempts, cancellationToken);

        MessagingDiagnostics.SetOutboxPending(module, pending);
        MessagingDiagnostics.SetOutboxFailed(module, failed);
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
