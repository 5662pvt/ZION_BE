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
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RabbitMqConsumerService> _logger;
    private IModel? _channel;

    public RabbitMqConsumerService(
        RabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        IntegrationEventTypeRegistry registry,
        IServiceScopeFactory scopeFactory,
        ILogger<RabbitMqConsumerService> logger)
    {
        _connection = connection;
        _options = options.Value;
        _registry = registry;
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
        _channel = _connection.GetConnection().CreateModel();
        _channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);
        _channel.QueueDeclare(_options.QueueName, durable: true, exclusive: false, autoDelete: false);
        _channel.QueueDeclare(DeadLetterQueueName, durable: true, exclusive: false, autoDelete: false);

        foreach (var eventType in _registry.RegisteredTypes)
            _channel.QueueBind(_options.QueueName, _options.Exchange, eventType.Name);

        _channel.BasicQos(0, prefetchCount: 20, global: false);
    }

    private async Task OnMessageReceivedAsync(object sender, BasicDeliverEventArgs args)
    {
        var routingKey = args.RoutingKey;
        try
        {
            if (!_registry.TryGetType(routingKey, out var eventType))
            {
                _logger.LogWarning("No registered type for routing key {RoutingKey}; dead-lettering", routingKey);
                DeadLetter(args);
                _channel!.BasicAck(args.DeliveryTag, multiple: false);
                return;
            }

            var json = Encoding.UTF8.GetString(args.Body.Span);
            var @event = (IntegrationEvent)(JsonSerializer.Deserialize(json, eventType)
                ?? throw new InvalidOperationException($"Message for '{routingKey}' deserialized to null."));

            using var scope = _scopeFactory.CreateScope();
            var handlerType = typeof(IIntegrationEventHandler<>).MakeGenericType(eventType);
            var handlers = scope.ServiceProvider.GetServices(handlerType);
            var handleMethod = handlerType.GetMethod(nameof(IIntegrationEventHandler<IntegrationEvent>.HandleAsync))!;

            foreach (var handler in handlers)
            {
                await (Task)handleMethod.Invoke(handler, new object[] { @event, CancellationToken.None })!;
            }

            _channel!.BasicAck(args.DeliveryTag, multiple: false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process message {RoutingKey} (EventId header {MessageId})", routingKey, args.BasicProperties?.MessageId);
            RetryOrDeadLetter(args);
            _channel!.BasicAck(args.DeliveryTag, multiple: false);
        }
    }

    private void RetryOrDeadLetter(BasicDeliverEventArgs args)
    {
        var retryCount = GetRetryCount(args.BasicProperties) + 1;
        if (retryCount > _options.RetryCount)
        {
            _logger.LogWarning("Message {RoutingKey} exceeded retry count {RetryCount}; dead-lettering", args.RoutingKey, _options.RetryCount);
            DeadLetter(args);
            return;
        }

        var props = _channel!.CreateBasicProperties();
        props.Persistent = true;
        props.MessageId = args.BasicProperties?.MessageId;
        props.Type = args.BasicProperties?.Type;
        props.Headers = new Dictionary<string, object> { [RetryCountHeader] = retryCount };

        _channel.BasicPublish(_options.Exchange, args.RoutingKey, props, args.Body);
    }

    private void DeadLetter(BasicDeliverEventArgs args) =>
        _channel!.BasicPublish(string.Empty, DeadLetterQueueName, args.BasicProperties, args.Body);

    private string DeadLetterQueueName => $"{_options.QueueName}.dlq";

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
