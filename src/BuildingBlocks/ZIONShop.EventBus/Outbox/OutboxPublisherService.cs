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

        // Row-level claim so multiple instances of this service (API + Worker, or several Worker
        // replicas) can run PublishBatchAsync concurrently against the same table without
        // double-publishing: FOR UPDATE SKIP LOCKED lets each instance grab a disjoint batch instead
        // of blocking on (or re-selecting) rows another instance already claimed. The lock is only
        // meaningful for the lifetime of one DB transaction, so claim + publish + status update +
        // commit all happen inside it.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        List<OutboxMessage> messages;
        try
        {
            var entityType = db.Model.FindEntityType(typeof(OutboxMessage))
                ?? throw new InvalidOperationException(
                    $"{nameof(OutboxMessage)} is not part of the {typeof(TDbContext).Name} model.");
            var tableRef = $"\"{entityType.GetSchema()}\".\"{entityType.GetTableName()}\"";

            // {{0}}/{{1}} (escaped braces in this verbatim interpolated string) become the literal
            // tokens "{0}"/"{1}" that FromSqlRaw itself resolves into real parameters -- tableRef is
            // the only part substituted by C# interpolation, and it comes from trusted model
            // metadata, never user input.
            var sql = $@"
SELECT * FROM {tableRef}
WHERE ""ProcessedOn"" IS NULL AND ""Attempts"" < {{0}}
ORDER BY ""OccurredOn""
LIMIT {{1}}
FOR UPDATE SKIP LOCKED";

            messages = await db.Set<OutboxMessage>()
                .FromSqlRaw(sql, MaxAttempts, BatchSize)
                .ToListAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        if (messages.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            await UpdateOutboxGaugesAsync(db, cancellationToken);
            return;
        }

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

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        await UpdateOutboxGaugesAsync(db, cancellationToken);
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
