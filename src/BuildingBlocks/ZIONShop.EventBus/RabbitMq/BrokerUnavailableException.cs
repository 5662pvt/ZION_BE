namespace ZIONShop.EventBus.RabbitMq;

/// <summary>
/// The RabbitMQ broker could not be reached (connection refused/unreachable, channel closed,
/// socket failure). Distinct from a publish that was rejected or timed out (nack, unroutable,
/// confirm timeout) so <c>OutboxPublisherService</c> can stop a batch early instead of burning
/// retry attempts while the broker is down.
/// </summary>
public sealed class BrokerUnavailableException : Exception
{
    public BrokerUnavailableException(string message) : base(message)
    {
    }

    public BrokerUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
