using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text;
using ZIONShop.EventBus.Inbox;

namespace ZIONShop.EventBus.Diagnostics;

/// <summary>
/// Shared ActivitySource/Meter for cross-cutting messaging telemetry (outbox publish, RabbitMQ
/// publish/consume, InMemory dispatch). Static so every piece of the EventBus -- regardless of DI
/// scope -- reports into the same instruments; the host application wires an actual OpenTelemetry
/// SDK exporter against these names in a later wave (this project takes no OpenTelemetry SDK
/// dependency itself, only System.Diagnostics).
/// </summary>
public static class MessagingDiagnostics
{
    public const string SourceName = "ZIONShop.Messaging";

    public static readonly ActivitySource ActivitySource = new(SourceName);

    private static readonly Meter Meter = new(SourceName);

    public static readonly Counter<long> Published = Meter.CreateCounter<long>("messaging.published");
    public static readonly Counter<long> Consumed = Meter.CreateCounter<long>("messaging.consumed");
    public static readonly Counter<long> HandlerFailed = Meter.CreateCounter<long>("messaging.handler.failed");
    public static readonly Counter<long> Retried = Meter.CreateCounter<long>("messaging.retried");
    public static readonly Counter<long> DeadLettered = Meter.CreateCounter<long>("messaging.dead_lettered");

    public static readonly Histogram<double> HandlerDuration =
        Meter.CreateHistogram<double>("messaging.handler.duration", unit: "ms");

    private static readonly ConcurrentDictionary<string, long> OutboxPendingByModule = new();
    private static readonly ConcurrentDictionary<string, long> OutboxFailedByModule = new();
    private static readonly ConcurrentDictionary<string, long> QueueMessagesByQueue = new();

#pragma warning disable IDE0052 // kept alive for its side effect of registering the callback with the Meter
    private static readonly ObservableGauge<long> OutboxPendingGauge =
        Meter.CreateObservableGauge("outbox.pending", () => Snapshot(OutboxPendingByModule, "module"));

    private static readonly ObservableGauge<long> OutboxFailedGauge =
        Meter.CreateObservableGauge("outbox.failed", () => Snapshot(OutboxFailedByModule, "module"));

    private static readonly ObservableGauge<long> QueueMessagesGauge =
        Meter.CreateObservableGauge("rabbitmq.queue.messages", () => Snapshot(QueueMessagesByQueue, "queue"));
#pragma warning restore IDE0052

    static MessagingDiagnostics()
    {
        // ASP.NET Core's host already defaults to W3C; forcing it here too covers every other
        // process that loads this building block (background workers, tests) without that pipeline,
        // so Activity.Current?.Id is always a parseable W3C traceparent wherever it is read.
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    public static void SetOutboxPending(string module, long value) => OutboxPendingByModule[module] = value;

    public static void SetOutboxFailed(string module, long value) => OutboxFailedByModule[module] = value;

    public static void SetQueueMessages(string queue, long value) => QueueMessagesByQueue[queue] = value;

    public static bool TryParseTraceParent(string? traceParent, string? traceState, out ActivityContext context)
    {
        if (!string.IsNullOrEmpty(traceParent) && ActivityContext.TryParse(traceParent, traceState, out context))
            return true;
        context = default;
        return false;
    }

    /// <summary>Reads a header value that may have arrived as a <see cref="string"/> (set by our own
    /// publisher, in-process) or as a <see cref="byte"/>[] (as RabbitMQ.Client deserializes AMQP
    /// table values received from the wire).</summary>
    public static string? ExtractHeaderString(IDictionary<string, object>? headers, string key)
    {
        if (headers is null || !headers.TryGetValue(key, out var value)) return null;
        return value switch
        {
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => null
        };
    }

    /// <summary>Extracts the W3C traceparent/tracestate from a RabbitMQ message's headers, for use as
    /// the parent of the consumer's "process" activity. Returns <c>default</c> (no parent) if absent
    /// or unparseable.</summary>
    public static ActivityContext ExtractParentContext(IDictionary<string, object>? headers)
    {
        var traceParent = ExtractHeaderString(headers, "traceparent");
        var traceState = ExtractHeaderString(headers, "tracestate");
        return TryParseTraceParent(traceParent, traceState, out var context) ? context : default;
    }

    /// <summary>Marks the activity Error and records an OTel semantic-convention "exception" event.
    /// Safe to call with a null activity (e.g. no listener subscribed, so StartActivity returned null).</summary>
    public static void RecordException(Activity? activity, Exception ex)
    {
        if (activity is null) return;
        activity.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity.AddEvent(new ActivityEvent("exception", tags: new ActivityTagsCollection
        {
            ["exception.type"] = ex.GetType().FullName,
            ["exception.message"] = ex.Message,
            ["exception.stacktrace"] = ex.ToString()
        }));
    }

    private static IEnumerable<Measurement<long>> Snapshot(ConcurrentDictionary<string, long> values, string tagName) =>
        values.Select(kv => new Measurement<long>(kv.Value, new KeyValuePair<string, object?>(tagName, kv.Key)));

    /// <summary>Name to report for handler telemetry (activity name, <c>consumer</c> metric tag, log
    /// messages): when <paramref name="handler"/> was resolved as an <see cref="IdempotentIntegrationEventHandler{TEvent,THandler,TDbContext}"/>
    /// inbox decorator, every handler would otherwise report the same generic decorator type name
    /// (e.g. <c>IdempotentIntegrationEventHandler`3</c>), hiding which consumer actually ran -- so this
    /// resolves to the decorated <c>THandler</c> instead, qualified with its owning module (the
    /// namespace segment right after "ZIONShop."), e.g. "Inventory.OrderCreatedIntegrationEventHandler".
    /// A handler that isn't decorated reports its own plain type name, unchanged.</summary>
    public static string ResolveHandlerName(object handler)
    {
        var type = handler.GetType();
        if (!type.IsGenericType || type.GetGenericTypeDefinition() != typeof(IdempotentIntegrationEventHandler<,,>))
            return type.Name;

        var innerHandlerType = type.GetGenericArguments()[1];
        const string modulePrefix = "ZIONShop.";
        var ns = innerHandlerType.Namespace ?? string.Empty;
        if (!ns.StartsWith(modulePrefix, StringComparison.Ordinal))
            return innerHandlerType.Name;

        var module = ns[modulePrefix.Length..].Split('.')[0];
        return $"{module}.{innerHandlerType.Name}";
    }
}
