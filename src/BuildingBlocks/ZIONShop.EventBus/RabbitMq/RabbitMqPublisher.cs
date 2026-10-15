using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using System.Diagnostics;
using System.Net.Sockets;
using ZIONShop.EventBus.Registry;

namespace ZIONShop.EventBus.RabbitMq;

/// <summary>
/// Single long-lived confirm-mode channel shared by every publish: integration events published
/// via <see cref="RabbitMqEventBus"/>, and the consumer's retry republishes
/// (<see cref="PublishToRetryQueueAsync"/>). A publisher confirm only proves the broker accepted
/// the message -- it says nothing about whether the message was routed anywhere. Mandatory
/// publish + Basic.Return catches "accepted but unroutable" so the caller (outbox/consumer) can
/// retry instead of silently losing the event.
/// </summary>
public sealed class RabbitMqPublisher : IDisposable
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly IntegrationEventTypeRegistry _registry;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IModel? _channel;

    // Basic.Return for a mandatory-and-unroutable message is delivered by the broker before the
    // confirm for the same delivery tag, and every publish holds _gate for its whole
    // publish+confirm round trip, so a single field read right after WaitForConfirmsOrDie is safe.
    private volatile string? _unroutableMessageId;

    public RabbitMqPublisher(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        IntegrationEventTypeRegistry registry,
        ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _options = options.Value;
        _registry = registry;
        _logger = logger;
    }

    /// <summary>Publishes an integration event to the main topic exchange, routing key = event type name.</summary>
    public Task PublishEventAsync(string routingKey, string messageId, byte[] body, CancellationToken cancellationToken)
    {
        var mandatory = _registry.TryGetType(routingKey, out _);
        return PublishAsync(_options.Exchange, routingKey, mandatory, messageId, routingKey, body, headers: null, cancellationToken);
    }

    /// <summary>
    /// Publishes a delayed-retry copy to <c>{QueueName}.retry.{attempt}</c> via the default
    /// exchange (routing key = queue name always resolves there), carrying <c>x-retry-count</c>
    /// and a truncated <c>x-last-error</c> header for diagnostics.
    /// </summary>
    /// <param name="incomingHeaders">The failed delivery's own headers (if any) -- copied forward as
    /// a baseline so the retry keeps carrying its original <c>traceparent</c>/<c>tracestate</c> even if
    /// no activity happens to be current at republish time; <see cref="Publish"/> still overlays the
    /// live <see cref="Activity.Current"/> trace context on top when one is present.</param>
    public Task PublishToRetryQueueAsync(int attempt, string messageId, string typeName, byte[] body,
        string lastError, IDictionary<string, object>? incomingHeaders, CancellationToken cancellationToken)
    {
        var routingKey = RabbitMqTopology.RetryQueueName(_options.QueueName, attempt);
        var headers = incomingHeaders is { Count: > 0 }
            ? new Dictionary<string, object>(incomingHeaders)
            : new Dictionary<string, object>();
        headers["x-retry-count"] = attempt;
        headers["x-last-error"] = Truncate(lastError, 500);
        return PublishAsync(string.Empty, routingKey, mandatory: true, messageId, typeName, body, headers, cancellationToken);
    }

    /// <summary>
    /// Republishes a message straight to the main exchange (used by the DLQ admin "requeue" action),
    /// routing key = original event type name. Forwards the failed delivery's own headers minus
    /// <c>x-death</c>/<c>x-retry-count</c>/<c>x-last-error</c> so the next delivery looks like a fresh
    /// publish rather than a retry -- in particular this still carries forward the original
    /// <c>traceparent</c>/<c>tracestate</c> headers (see <see cref="PublishToRetryQueueAsync"/> for the
    /// same reasoning), overlaid by the live <see cref="Activity.Current"/> trace context if one happens
    /// to be present at requeue time.
    /// </summary>
    public Task PublishRequeuedAsync(string messageId, string typeName, byte[] body,
        IDictionary<string, object>? incomingHeaders, CancellationToken cancellationToken)
    {
        var headers = incomingHeaders is { Count: > 0 }
            ? new Dictionary<string, object>(incomingHeaders)
            : new Dictionary<string, object>();
        headers.Remove("x-death");
        headers.Remove("x-retry-count");
        headers.Remove("x-last-error");
        return PublishAsync(_options.Exchange, typeName, mandatory: true, messageId, typeName, body,
            headers.Count > 0 ? headers : null, cancellationToken);
    }

    private async Task PublishAsync(string exchange, string routingKey, bool mandatory, string messageId, string typeName,
        byte[] body, IDictionary<string, object>? headers, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Publish(exchange, routingKey, mandatory, messageId, typeName, body, headers);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Publish(string exchange, string routingKey, bool mandatory, string messageId, string typeName,
        byte[] body, IDictionary<string, object>? headers)
    {
        var channel = GetOrCreateChannel();

        var props = channel.CreateBasicProperties();
        props.Persistent = true;
        props.MessageId = messageId;
        props.Type = typeName;
        props.ContentType = "application/json";
        props.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        // Propagate the live trace context (if any -- e.g. the outbox's "{EventType} publish" activity,
        // or the consumer's "{EventType} process" activity when this is a retry-queue republish) onto
        // the outgoing message so the next hop can continue the same trace.
        if (Activity.Current is { } activity)
        {
            headers = headers is { Count: > 0 } ? new Dictionary<string, object>(headers) : new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(activity.Id)) headers["traceparent"] = activity.Id;
            if (!string.IsNullOrEmpty(activity.TraceStateString)) headers["tracestate"] = activity.TraceStateString;
        }
        if (headers is { Count: > 0 }) props.Headers = headers;

        Activity.Current?.SetTag("messaging.system", "rabbitmq");
        Activity.Current?.SetTag("messaging.destination.name", exchange);
        Activity.Current?.SetTag("messaging.message.id", messageId);
        Activity.Current?.SetTag("messaging.operation", "publish");

        _unroutableMessageId = null;

        try
        {
            channel.BasicPublish(exchange, routingKey, mandatory, props, body);
            // WaitForConfirmsOrDie closes the channel itself and throws IOException on a nack, or
            // TimeoutException if the confirm never arrives -- both are broker/transport stalls, not
            // this message's fault, so they must map to BrokerUnavailableException the same as a
            // channel already closed out from under us (AlreadyClosedException) or interrupted mid-call
            // (OperationInterruptedException), not to a plain publish-rejected failure.
            channel.WaitForConfirmsOrDie(TimeSpan.FromSeconds(Math.Max(1, _options.ConfirmTimeoutSeconds)));
        }
        catch (Exception ex) when (ex is AlreadyClosedException or IOException or TimeoutException or OperationInterruptedException)
        {
            InvalidateChannel();
            throw new BrokerUnavailableException("RabbitMQ channel closed unexpectedly while publishing.", ex);
        }

        if (string.Equals(_unroutableMessageId, messageId, StringComparison.Ordinal))
        {
            _unroutableMessageId = null;
            throw new InvalidOperationException(
                $"Message '{messageId}' ({typeName}) was confirmed by the broker but not routed to any queue " +
                $"(unroutable for exchange '{exchange}', routing key '{routingKey}').");
        }
    }

    private IModel GetOrCreateChannel()
    {
        if (_channel is { IsOpen: true }) return _channel;

        try
        {
            if (!_connection.TryGetConnection(out var connection))
                throw new BrokerUnavailableException("RabbitMQ broker is unavailable.");

            var channel = connection.CreateModel();
            channel = RabbitMqTopology.Declare(connection, channel, _options, _registry, _logger);
            channel.ConfirmSelect();
            channel.BasicReturn += OnBasicReturn;
            _channel = channel;
            return channel;
        }
        catch (BrokerUnavailableException)
        {
            throw;
        }
        catch (Exception ex) when (ex is BrokerUnreachableException or AlreadyClosedException or SocketException
            or ObjectDisposedException or OperationInterruptedException or IOException or TimeoutException)
        {
            // Covers every way acquiring the connection/channel or declaring topology can fail while
            // the broker is down or recovering -- including ObjectDisposedException, which
            // RabbitMqConnection's IsConnected/IsRecovering guard against internally but a connection
            // that *was* legitimately disposed (app shutdown) can still surface here.
            throw new BrokerUnavailableException("RabbitMQ broker is unavailable.", ex);
        }
    }

    private void OnBasicReturn(object? sender, BasicReturnEventArgs args) =>
        _unroutableMessageId = args.BasicProperties?.MessageId;

    private void InvalidateChannel()
    {
        try
        {
            _channel?.Dispose();
        }
        catch
        {
            // Already closed by the broker.
        }
        _channel = null;
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    public void Dispose()
    {
        _gate.Dispose();
        try
        {
            _channel?.Dispose();
        }
        catch
        {
            // Ignore -- disposing a connection/channel that is already closed is a no-op anyway.
        }
    }
}
