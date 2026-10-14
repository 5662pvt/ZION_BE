using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Registry;

namespace ZIONShop.EventBus.RabbitMq;

public class RabbitMqConsumerService : BackgroundService
{
    private const string RetryCountHeader = "x-retry-count";
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);

    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly IntegrationEventTypeRegistry _registry;
    private readonly RabbitMqPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private IModel? _channel;
    private CancellationToken _stoppingToken;

    public RabbitMqConsumerService(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        IntegrationEventTypeRegistry registry,
        RabbitMqPublisher publisher,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumerService> logger)
    {
        _connection = connection;
        _options = options.Value;
        _registry = registry;
        _publisher = publisher;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_registry.RegisteredTypes.Count == 0)
        {
            _logger.LogInformation("No integration event handlers registered; RabbitMQ consumer stays idle.");
            return;
        }

        _stoppingToken = stoppingToken;
        await StartConsumingWithRetryAsync(stoppingToken);
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
            TryNack(args.DeliveryTag, requeue: false);
            return;
        }

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
                try
                {
                    await (Task)handleMethod.Invoke(handler, new object[] { @event, CancellationToken.None })!;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Handler {Handler} failed for {EventType} (MessageId {MessageId})",
                        handler?.GetType().Name, resolvedTypeName, args.BasicProperties?.MessageId);
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
            await HandleFailureAsync(args, resolvedTypeName, failure);
            return;
        }

        TryAck(args.DeliveryTag);
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
            TryNack(args.DeliveryTag, requeue: false);
            return;
        }

        try
        {
            var messageId = args.BasicProperties?.MessageId ?? Guid.NewGuid().ToString();
            await _publisher.PublishToRetryQueueAsync(attempt, messageId, typeName, args.Body.ToArray(), ex.Message, CancellationToken.None);
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
