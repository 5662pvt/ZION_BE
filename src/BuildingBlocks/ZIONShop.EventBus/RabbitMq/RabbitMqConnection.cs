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
            AutomaticRecoveryEnabled = true,
            // Bounds a failed connection attempt (e.g. broker down/paused) so the publish fast
            // path (TryGetConnection) never hangs anywhere near as long as the consumer's own
            // 2+4+8+16+32s startup backoff below.
            RequestedConnectionTimeout = TimeSpan.FromSeconds(5)
        };
    }

    public bool IsConnected => _connection is { IsOpen: true };

    /// <summary>
    /// Blocking, retries with exponential backoff. Only for the consumer's background startup,
    /// which can afford to wait for the broker to come back online.
    /// </summary>
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

    /// <summary>
    /// Single attempt, bounded by <see cref="ConnectionFactory.RequestedConnectionTimeout"/>. For
    /// the publish path: it must fail fast instead of blocking the outbox loop for up to a minute
    /// when the broker is down. Mirrors the <c>TryGetType</c>-style out-pattern used elsewhere in
    /// this codebase (e.g. <see cref="Registry.IntegrationEventTypeRegistry.TryGetType"/>).
    /// </summary>
    public bool TryGetConnection(out IConnection connection)
    {
        if (IsConnected)
        {
            connection = _connection!;
            return true;
        }

        lock (_lock)
        {
            if (IsConnected)
            {
                connection = _connection!;
                return true;
            }

            try
            {
                _connection = _factory.CreateConnection(_options.QueueName);
                connection = _connection;
                return true;
            }
            catch (Exception ex) when (ex is BrokerUnreachableException or SocketException)
            {
                _logger.LogWarning(ex, "RabbitMQ broker unavailable for publish");
                connection = null!;
                return false;
            }
        }
    }

    public void Dispose() => _connection?.Dispose();
}
