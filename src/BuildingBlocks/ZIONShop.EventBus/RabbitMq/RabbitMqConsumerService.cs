using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Registry;

namespace ZIONShop.EventBus.RabbitMq;

public class RabbitMqConsumerService : BackgroundService
{
    private const string RetryCountHeader = "x-retry-count";

    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly IntegrationEventTypeRegistry _registry;
    private readonly RabbitMqPublisher _publisher;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private IModel? _channel;

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

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_registry.RegisteredTypes.Count == 0)
        {
            _logger.LogInformation("No integration event handlers registered; RabbitMQ consumer stays idle.");
            return Task.CompletedTask;
        }

        var policy = Policy
            .Handle<BrokerUnreachableException>()
            .Or<SocketException>()
            .WaitAndRetry(_options.RetryCount,
                attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                (ex, ts) => _logger.LogWarning(ex, "RabbitMQ consumer connection retry in {Delay}s", ts.TotalSeconds));

        policy.Execute(Initialize);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += OnMessageReceivedAsync;
        _channel!.BasicConsume(_options.QueueName, autoAck: false, consumer);

        return Task.CompletedTask;
    }

    private void Initialize()
    {
        var connection = _connection.GetConnection();
        var channel = connection.CreateModel();
        channel = RabbitMqTopology.Declare(connection, channel, _options, _registry, _logger);
        channel.BasicQos(0, prefetchCount: (ushort)_options.PrefetchCount, global: false);
        _channel = channel;
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
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
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
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var handlers = scope.ServiceProvider.GetServices(handlerType);
            var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.HandleAsync))!;

            foreach (var handler in handlers)
                await (Task)handleMethod.Invoke(handler, new object[] { @event, CancellationToken.None })!;

            _channel!.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            await HandleFailureAsync(args, resolvedTypeName, ex);
        }
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
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            return;
        }

        try
        {
            var messageId = args.BasicProperties?.MessageId ?? Guid.NewGuid().ToString();
            await _publisher.PublishToRetryQueueAsync(attempt, messageId, typeName, args.Body.ToArray(), ex.Message, CancellationToken.None);
            _channel!.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception republishEx)
        {
            _logger.LogError(republishEx,
                "Failed to republish {MessageId} ({TypeName}) to the retry queue; requeueing on the main queue",
                args.BasicProperties?.MessageId, typeName);
            _channel!.BasicNack(args.DeliveryTag, multiple: false, requeue: true);
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
