using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Diagnostics;

namespace ZIONShop.EventBus.InMemory;

public class InMemoryEventBus : IEventBus
{
    private readonly IServiceProvider _provider;
    private readonly ILogger<InMemoryEventBus> _logger;

    public InMemoryEventBus(IServiceProvider provider, ILogger<InMemoryEventBus> logger)
    {
        _provider = provider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
    {
        var eventTypeName = typeof(TEvent).Name;

        using var scope = _provider.CreateScope();
        var handlers = scope.ServiceProvider.GetServices<IIntegrationEventHandler<TEvent>>().ToList();
        if (handlers.Count == 0)
        {
            _logger.LogDebug("No handler for {EventType}", eventTypeName);
            return;
        }

        using var logScope = IntegrationEventLogScope.Begin(_logger, @event.EventId, eventTypeName, @event);

        foreach (var handler in handlers)
        {
            var handlerName = MessagingDiagnostics.ResolveHandlerName(handler);

            // Parent = Activity.Current, whatever it already is (e.g. the outbox's
            // "{EventType} publish" producer activity wrapping this whole dispatch) -- no separate
            // "process" activity is needed here, unlike the RabbitMQ consumer, since there is no
            // transport hop to re-anchor the trace to.
            using var activity = MessagingDiagnostics.ActivitySource.StartActivity(handlerName, ActivityKind.Internal);
            activity?.SetTag("messaging.message.id", @event.EventId.ToString());
            activity?.SetTag("event.type", eventTypeName);

            var stopwatch = Stopwatch.StartNew();
            try
            {
                await handler.HandleAsync(@event, cancellationToken);
                stopwatch.Stop();
                MessagingDiagnostics.HandlerDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("event.type", eventTypeName),
                    new KeyValuePair<string, object?>("consumer", handlerName),
                    new KeyValuePair<string, object?>("outcome", "success"));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                MessagingDiagnostics.HandlerDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
                    new KeyValuePair<string, object?>("event.type", eventTypeName),
                    new KeyValuePair<string, object?>("consumer", handlerName),
                    new KeyValuePair<string, object?>("outcome", "failure"));
                MessagingDiagnostics.RecordException(activity, ex);
                MessagingDiagnostics.HandlerFailed.Add(1,
                    new KeyValuePair<string, object?>("event.type", eventTypeName),
                    new KeyValuePair<string, object?>("consumer", handlerName));

                _logger.LogError(ex, "Integration handler {Handler} failed for {EventType}", handlerName, eventTypeName);
            }
        }

        MessagingDiagnostics.Consumed.Add(1, new KeyValuePair<string, object?>("event.type", eventTypeName));
    }
}
