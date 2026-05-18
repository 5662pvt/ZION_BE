using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.RabbitMq;

public class RabbitMqEventBus : IEventBus
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqEventBus> _logger;

    public RabbitMqEventBus(RabbitMqConnection connection, IOptions<RabbitMqOptions> options, ILogger<RabbitMqEventBus> logger)
    {
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : IntegrationEvent
    {
        try
        {
            using var channel = _connection.GetConnection().CreateModel();
            channel.ExchangeDeclare(_options.Exchange, ExchangeType.Topic, durable: true);

            var routingKey = typeof(TEvent).Name;
            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize<object>(@event));

            var props = channel.CreateBasicProperties();
            props.Persistent = true;
            props.MessageId = @event.EventId.ToString();
            props.Type = routingKey;

            channel.BasicPublish(_options.Exchange, routingKey, props, body);
            _logger.LogInformation("Published {EventType} to {Exchange}/{RoutingKey}", routingKey, _options.Exchange, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish {EventType}; falling back silently", typeof(TEvent).Name);
        }
        return Task.CompletedTask;
    }
}
