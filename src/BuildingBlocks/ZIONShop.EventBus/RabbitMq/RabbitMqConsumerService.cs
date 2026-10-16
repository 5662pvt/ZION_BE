using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Diagnostics;
using ZIONShop.EventBus.FaultInjection;
using ZIONShop.EventBus.Registry;

namespace ZIONShop.EventBus.RabbitMq;

public class RabbitMqConsumerService : BackgroundService
{
    private const string RetryCountHeader = "x-retry-count";
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan QueueMetricsRefreshInterval = TimeSpan.FromSeconds(15);

    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly IntegrationEventTypeRegistry _registry;
    private readonly RabbitMqPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IFaultInjector _faultInjector;
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private IModel? _channel;
    private CancellationToken _stoppingToken;

    // IFaultInjector is optional: hosts that compose this service without going through
    // AddZionEventBus' RabbitMq branch (e.g. a test host building its own ServiceCollection) never
    // register it. MS DI falls back to this default when the service isn't registered, so the
    // consumer still activates there -- it just never crashes on purpose.
    public RabbitMqConsumerService(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        IntegrationEventTypeRegistry registry,
        RabbitMqPublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumerService> logger,
        IFaultInjector? faultInjector = null)
    {
        _connection = connection;
        _options = options.Value;
        _registry = registry;
        _publisher = publisher;
        _scopeFactory = scopeFactory;
        _faultInjector = faultInjector ?? NoOpFaultInjector.Instance;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        // Fire-and-forget: this loop owns its own cadence/error handling (it never throws) and must
        // keep refreshing queue depth regardless of whether this instance ends up consuming anything.
        _ = RefreshQueueMetricsLoopAsync(stoppingToken);

        if (_registry.RegisteredTypes.Count == 0)
        {
            _logger.LogInformation("No integration event handlers registered; RabbitMQ consumer stays idle.");
            return;
        }

        await StartConsumingWithRetryAsync(stoppingToken);
    }

    /// <summary>Refreshes <c>rabbitmq.queue.messages</c> (tags <c>queue=main|dlq</c>) every
    /// <see cref="QueueMetricsRefreshInterval"/> using a passive declare on its own short-lived
    /// channel, so it never competes with the consumer's own channel/consumer tag. Silently skips a
    /// tick if the broker or queue is unavailable -- that is itself useful information (reflected by
    /// the gauge simply not advancing), not a reason to log or crash.</summary>
    private async Task RefreshQueueMetricsLoopAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_connection.TryGetConnection(out var connection))
            {
                TryUpdateQueueDepth(connection, _options.QueueName, "main");
                TryUpdateQueueDepth(connection, RabbitMqTopology.DeadLetterQueueName(_options.QueueName), "dlq");
            }

            try
            {
                await Task.Delay(QueueMetricsRefreshInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static void TryUpdateQueueDepth(IConnection connection, string queueName, string queueTag)
    {
        IModel? channel = null;
        try
        {
            channel = connection.CreateModel();
            var result = channel.QueueDeclarePassive(queueName);
            MessagingDiagnostics.SetQueueMessages(queueTag, result.MessageCount);
        }
        catch
        {
            // Queue not declared yet, or the broker hiccuped -- skip this tick silently, retry next one.
        }
        finally
        {
            try { channel?.Dispose(); } catch { /* a failed passive declare already closed it */ }
        }
    }

    /// <summary>
    /// Keeps retrying (re)initialization with a capped exponential backoff (2,4,8,16,30,30,...s)
    /// until it succeeds or the service is stopping -- never gives up after a single failure. Covers
    /// both the very first start happening while the broker is down (it must start consuming once the
    /// broker comes up) and reinitialization triggered by <see cref="OnConsumerCancelledAsync"/>.
    /// </summary>
    private async Task StartConsumingWithRetryAsync(CancellationToken stoppingToken)
    {
        var delaySeconds = 2;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                StartConsuming();
                return;
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to (re)initialize RabbitMQ consumer; retrying in {Delay}s", delaySeconds);
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            delaySeconds = Math.Min(delaySeconds * 2, (int)MaxReconnectDelay.TotalSeconds);
        }
    }

    /// <summary>(Re)connects, (re)declares the topology and starts a fresh consumer. A single attempt --
    /// retries/backoff are the caller's (<see cref="StartConsumingWithRetryAsync"/>) job.</summary>
    private void StartConsuming()
    {
        Initialize();

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceivedAsync;
        consumer.ConsumerCancelled += OnConsumerCancelledAsync;
        _channel!.BasicConsume(_options.QueueName, autoAck: false, consumer);
    }

    private void Initialize()
    {
        var connection = _connection.GetConnection();
        var channel = connection.CreateModel();
        channel = RabbitMqTopology.Declare(connection, channel, _options, _registry, _logger);
        channel.BasicQos(0, prefetchCount: (ushort)_options.PrefetchCount, global: false);

        if (_channel is not null)
        {
            try { _channel.Dispose(); } catch { /* already closed by the broker */ }
        }

        _channel = channel;
    }

    /// <summary>
    /// The broker can cancel our consumer in two different situations that must be told apart:
    /// (1) the connection itself went down (broker restart/forced close) -- RabbitMQ.Client's own
    /// <c>AutomaticRecoveryEnabled</c> + topology recovery will redeclare the queue/bindings and
    /// re-register this very consumer once it reconnects, so reinitializing manually here would race
    /// that recovery (and, if it disposed the connection, break it -- see <see cref="RabbitMqConnection"/>);
    /// (2) the connection is still up but just this consumer was cancelled (queue deleted/redeclared by
    /// another instance, admin action, etc.) -- autorecovery can't help here since nothing about the
    /// connection changed, so we must reconnect/redeclare/re-consume ourselves.
    /// </summary>
    private async Task OnConsumerCancelledAsync(object sender, ConsumerEventArgs args)
    {
        if (!_connection.IsConnected)
        {
            _logger.LogWarning(
                "RabbitMQ consumer cancelled while the connection is down (consumer tag {ConsumerTag}); " +
                "leaving reconnection to the client's automatic recovery",
                string.Join(",", args.ConsumerTags));
            return;
        }

        _logger.LogCritical(
            "RabbitMQ consumer was cancelled by the broker while the connection is up (consumer tag {ConsumerTag}); reinitializing",
            string.Join(",", args.ConsumerTags));

        await StartConsumingWithRetryAsync(_stoppingToken);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        // The first delivery of an event carries Type = the real event type name and RoutingKey
        // equal to it too. A message coming back from a retry queue (after its TTL expired and it
        // was dead-lettered to the main queue) is redelivered with RoutingKey = the queue name
        // (see RabbitMqTopology's x-dead-letter-routing-key), so Type is the only reliable source
        // then -- it is preserved verbatim across every republish by RabbitMqPublisher.
        var typeName = args.BasicProperties?.Type;
        var resolvedTypeName = !string.IsNullOrEmpty(typeName) ? typeName : args.RoutingKey;

        if (!_registry.TryGetType(resolvedTypeName, out var eventType))
        {
            _logger.LogWarning(
                "No registered type for '{TypeName}' (routing key {RoutingKey}, MessageId {MessageId}); dead-lettering",
                resolvedTypeName, args.RoutingKey, args.BasicProperties?.MessageId);
            MessagingDiagnostics.DeadLettered.Add(1, new KeyValuePair<string, object?>("event.type", resolvedTypeName));
            TryNack(args.DeliveryTag, requeue: false);
            return;
        }

        IntegrationEvent @event;
        try
        {
            var json = Encoding.UTF8.GetString(args.Body.Span);
            @event = (IntegrationEvent)(JsonSerializer.Deserialize(json, eventType)
                ?? throw new InvalidOperationException($"Message for '{resolvedTypeName}' deserialized to null."));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Poison message for '{TypeName}' (MessageId {MessageId}); dead-lettering without retry",
                resolvedTypeName, args.BasicProperties?.MessageId);
            MessagingDiagnostics.DeadLettered.Add(1, new KeyValuePair<string, object?>("event.type", resolvedTypeName));
            TryNack(args.DeliveryTag, requeue: false);
            return;
        }

        var attempt = GetRetryCount(args.BasicProperties) + 1;
        var parentContext = MessagingDiagnostics.ExtractParentContext(args.BasicProperties?.Headers);

        // Kept current for the whole method body -- including through HandleFailureAsync below -- so a
        // retry-queue republish (via RabbitMqPublisher) picks it up as Activity.Current and keeps the
        // same TraceId as this delivery.
        using var activity = MessagingDiagnostics.ActivitySource.StartActivity(
            $"{resolvedTypeName} process", ActivityKind.Consumer, parentContext);
        activity?.SetTag("messaging.system", "rabbitmq");
        activity?.SetTag("messaging.destination.name", _options.QueueName);
        activity?.SetTag("messaging.operation", "process");
        activity?.SetTag("messaging.message.id", args.BasicProperties?.MessageId);
        activity?.SetTag("event.type", resolvedTypeName);
        activity?.SetTag("retry.attempt", attempt);

        using var logScope = IntegrationEventLogScope.Begin(_logger, @event.EventId, resolvedTypeName, @event);

        // Run every registered handler for this event type, even if an earlier one throws, so a failing
        // handler never prevents the others from applying their effect on this delivery -- a handler that
        // did succeed is skipped on redelivery by its own inbox row, but one that was never even invoked
        // would otherwise have to wait for every prior handler to become healthy first.
        Exception? failure = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var handlers = scope.ServiceProvider.GetServices(handlerType);
            var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.HandleAsync))!;

            foreach (var handler in handlers)
            {
                var handlerName = handler is not null ? MessagingDiagnostics.ResolveHandlerName(handler) : "Unknown";
                using var handlerActivity = MessagingDiagnostics.ActivitySource.StartActivity(handlerName, ActivityKind.Internal);
                handlerActivity?.SetTag("messaging.message.id", args.BasicProperties?.MessageId);
                handlerActivity?.SetTag("retry.attempt", attempt);

                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await (Task)handleMethod.Invoke(handler, new object[] { @event, CancellationToken.None })!;
                    RecordHandlerDuration(stopwatch, resolvedTypeName, handlerName, "success");
                }
                catch (Exception ex)
                {
                    RecordHandlerDuration(stopwatch, resolvedTypeName, handlerName, "failure");
                    MessagingDiagnostics.RecordException(handlerActivity, ex);
                    MessagingDiagnostics.HandlerFailed.Add(1,
                        new KeyValuePair<string, object?>("event.type", resolvedTypeName),
                        new KeyValuePair<string, object?>("consumer", handlerName));

                    _logger.LogError(ex,
                        "Handler {Handler} failed for {EventType} (MessageId {MessageId})",
                        handlerName, resolvedTypeName, args.BasicProperties?.MessageId);
                    failure ??= ex;
                }
            }
        }
        catch (Exception ex)
        {
            failure ??= ex;
        }

        if (failure is not null)
        {
            MessagingDiagnostics.RecordException(activity, failure);
            await HandleFailureAsync(args, resolvedTypeName, failure);
            return;
        }

        if (_faultInjector.ShouldCrashBeforeAck(resolvedTypeName))
        {
            // Crash() terminates the process (Environment.FailFast) and never returns in practice;
            // the return below only matters for unit tests driving this with a fake IProcessTerminator.
            _faultInjector.Crash(resolvedTypeName, args.BasicProperties?.MessageId);
            return;
        }

        MessagingDiagnostics.Consumed.Add(1, new KeyValuePair<string, object?>("event.type", resolvedTypeName));
        TryAck(args.DeliveryTag);
    }

    private static void RecordHandlerDuration(Stopwatch stopwatch, string eventType, string handlerName, string outcome)
    {
        stopwatch.Stop();
        MessagingDiagnostics.HandlerDuration.Record(stopwatch.Elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("event.type", eventType),
            new KeyValuePair<string, object?>("consumer", handlerName),
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    private async Task HandleFailureAsync(BasicDeliverEventArgs args, string typeName, Exception ex)
    {
        var attempt = GetRetryCount(args.BasicProperties) + 1;
        _logger.LogError(ex, "Failed to process message {MessageId} ({TypeName}), attempt {Attempt}",
            args.BasicProperties?.MessageId, typeName, attempt);

        if (attempt > _options.RetryCount)
        {
            _logger.LogWarning(
                "Message {MessageId} ({TypeName}) exceeded retry count {RetryCount}; dead-lettering",
                args.BasicProperties?.MessageId, typeName, _options.RetryCount);
            MessagingDiagnostics.DeadLettered.Add(1, new KeyValuePair<string, object?>("event.type", typeName));
            TryNack(args.DeliveryTag, requeue: false);
            return;
        }

        try
        {
            var messageId = args.BasicProperties?.MessageId ?? Guid.NewGuid().ToString();
            await _publisher.PublishToRetryQueueAsync(attempt, messageId, typeName, args.Body.ToArray(), ex.Message,
                args.BasicProperties?.Headers, CancellationToken.None);
            MessagingDiagnostics.Retried.Add(1, new KeyValuePair<string, object?>("event.type", typeName));
            TryAck(args.DeliveryTag);
        }
        catch (Exception republishEx)
        {
            _logger.LogError(republishEx,
                "Failed to republish {MessageId} ({TypeName}) to the retry queue; requeueing on the main queue",
                args.BasicProperties?.MessageId, typeName);

            // Without a delay, a broker/connection problem that keeps failing the republish would
            // otherwise have the consumer immediately redeliver-and-fail this same message in a tight
            // hot loop.
            await Task.Delay(TimeSpan.FromSeconds(1));
            TryNack(args.DeliveryTag, requeue: true);
        }
    }

    /// <summary>A failed ack/nack (e.g. the channel was just closed by the broker) is not a reason to
    /// treat an otherwise-successful delivery as failed -- the broker will simply redeliver, and the
    /// inbox on each handler dedupes that redelivery, so logging and moving on is enough.</summary>
    private void TryAck(ulong deliveryTag)
    {
        try
        {
            _channel!.BasicAck(deliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BasicAck failed for delivery {DeliveryTag}; broker will redeliver", deliveryTag);
        }
    }

    private void TryNack(ulong deliveryTag, bool requeue)
    {
        try
        {
            _channel!.BasicNack(deliveryTag, multiple: false, requeue: requeue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BasicNack failed for delivery {DeliveryTag}", deliveryTag);
        }
    }

    private static int GetRetryCount(IBasicProperties? props)
    {
        if (props?.Headers is { } headers && headers.TryGetValue(RetryCountHeader, out var value))
        {
            return value switch
            {
                int i => i,
                long l => (int)l,
                byte[] b => int.Parse(Encoding.UTF8.GetString(b)),
                _ => 0
            };
        }
        return 0;
    }

    public override void Dispose()
    {
        _channel?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }
}
