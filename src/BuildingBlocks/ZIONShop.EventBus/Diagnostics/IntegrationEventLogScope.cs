using System.Collections.Concurrent;
using System.Reflection;
using Microsoft.Extensions.Logging;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.Diagnostics;

/// <summary>
/// Pushes MessageId/EventType/OrderId (when the event carries one) onto the logger scope for the
/// duration of a publish or handler invocation, so every log line emitted underneath is enrichable by
/// them. Uses <see cref="ILogger.BeginScope{TState}"/> rather than Serilog's LogContext directly:
/// this project has no package reference on Serilog, and Serilog's own ILogger provider already
/// surfaces BeginScope state as LogContext properties, so the host's Serilog sink sees them either way.
/// </summary>
internal static class IntegrationEventLogScope
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo?> OrderIdProperties = new();

    public static IDisposable? Begin(ILogger logger, Guid messageId, string eventType, IntegrationEvent? @event)
    {
        var state = new Dictionary<string, object>
        {
            ["MessageId"] = messageId,
            ["EventType"] = eventType
        };

        if (@event is not null)
        {
            var property = OrderIdProperties.GetOrAdd(@event.GetType(), t => t.GetProperty("OrderId"));
            var orderId = property?.GetValue(@event);
            if (orderId is not null) state["OrderId"] = orderId;
        }

        return logger.BeginScope(state);
    }
}
