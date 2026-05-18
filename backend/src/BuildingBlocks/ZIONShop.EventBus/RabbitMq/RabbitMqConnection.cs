using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

namespace ZIONShop.EventBus.RabbitMq;

public class RabbitMqConnection : IDisposable
{
    private readonly IConnectionFactory _factory;
    private readonly ILogger<RabbitMqConnection> _logger;
    private readonly RabbitMqOptions _options;
    private IConnection? _connection;
    private readonly object _lock = new();

    public RabbitMqConnection(IOptions<RabbitMqOptions> options, ILogger<RabbitMqConnection> logger)
    {
        _options = options.Value;
        _logger = logger;
        _factory = new ConnectionFactory
        {
            HostName = _options.Host,
            Port = _options.Port,
            VirtualHost = _options.VirtualHost,
            UserName = _options.User,
            Password = _options.Pass,
            DispatchConsumersAsync = true,
            AutomaticRecoveryEnabled = true
        };
    }

    public bool IsConnected => _connection is { IsOpen: true };

    public IConnection GetConnection()
    {
        if (IsConnected) return _connection!;
        lock (_lock)
        {
            if (IsConnected) return _connection!;
            var policy = Policy
                .Handle<BrokerUnreachableException>()
                .Or<SocketException>()
                .WaitAndRetry(_options.RetryCount,
                    attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    (ex, ts) => _logger.LogWarning(ex, "RabbitMQ unreachable, retrying in {Delay}s", ts.TotalSeconds));
            policy.Execute(() => _connection = _factory.CreateConnection(_options.QueueName));
            return _connection ?? throw new InvalidOperationException("Could not create RabbitMQ connection.");
        }
    }

    public void Dispose() => _connection?.Dispose();
}
