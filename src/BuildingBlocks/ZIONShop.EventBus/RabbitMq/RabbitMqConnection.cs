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

    public bool IsConnected
    {
        get
        {
            var connection = _connection;
            if (connection is null) return false;
            try
            {
                return connection.IsOpen;
            }
            catch (ObjectDisposedException)
            {
                // Only reachable if Dispose() below ran concurrently with this read -- not usable.
                return false;
            }
        }
    }

    /// <summary>
    /// True while <see cref="_connection"/> is a still-usable (autorecovering) connection that is not
    /// open right now -- i.e. RabbitMQ.Client's own <c>AutomaticRecoveryEnabled</c> machinery owns it
    /// and is (or will be) reconnecting it in the background. Creating a second connection in this
    /// state would leak the old one (it keeps trying to recover, orphaned); disposing it out from
    /// under that recovery is worse -- the client's own recovery loop then throws
    /// <see cref="ObjectDisposedException"/> the next time it touches the connection. Either way: a
    /// connection that exists and isn't disposed is never ours to replace, only to wait for.
    /// </summary>
    private bool IsRecovering
    {
        get
        {
            var connection = _connection;
            if (connection is null) return false;
            try
            {
                return !connection.IsOpen;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }
    }

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
                .WaitAndRetry(_options.ConnectRetryCount,
                    attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
                    (ex, ts) => _logger.LogWarning(ex, "RabbitMQ unreachable, retrying in {Delay}s", ts.TotalSeconds));

            policy.Execute(() =>
            {
                if (IsConnected) return;

                if (IsRecovering)
                {
                    // Already have a connection quietly auto-recovering -- surface this as the same
                    // transient failure the policy already retries on, instead of touching (and
                    // breaking) a connection the client library still owns.
                    throw new BrokerUnreachableException(
                        new InvalidOperationException("RabbitMQ connection is auto-recovering."));
                }

                // Reaching here means _connection is null (first connect) or was disposed by
                // Dispose() below (app shutdown) -- the only cases where creating a fresh one is ours
                // to do. Never dispose the existing connection ourselves: a broker-initiated close
                // (e.g. CONNECTION_FORCED) is exactly what AutomaticRecoveryEnabled exists to recover
                // from on its own.
                _connection = _factory.CreateConnection(_options.QueueName);
            });

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

            if (IsRecovering)
            {
                // Wait for the existing connection to recover instead of touching (and breaking) it.
                connection = null!;
                return false;
            }

            // _connection is null or was disposed by Dispose() below -- safe to create a fresh one;
            // never dispose a connection the client library's own autorecovery still owns.
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

    public void Dispose()
    {
        try
        {
            _connection?.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
