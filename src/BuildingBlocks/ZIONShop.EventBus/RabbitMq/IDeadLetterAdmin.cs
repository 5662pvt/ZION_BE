namespace ZIONShop.EventBus.RabbitMq;

public record DeadLetterMessage(
    string? MessageId, string EventType, string Reason, int DeathCount,
    DateTimeOffset? FirstDeathAt, string? LastError, string Payload);

public record DeadLetterPeekResult(string Queue, long MessageCount, IReadOnlyList<DeadLetterMessage> Messages);

/// <summary>
/// Admin access to the dead-letter queue for the admin "Hệ thống" screens. The RabbitMQ
/// implementation (<see cref="RabbitMqDeadLetterAdmin"/>) talks to the broker directly on its own
/// short-lived channel; the InMemory provider (<see cref="InMemory.InMemoryDeadLetterAdmin"/>) has no
/// broker at all, so it reports an always-empty queue.
/// </summary>
public interface IDeadLetterAdmin
{
    /// <summary>
    /// PEEKs up to <paramref name="limit"/> messages (1-100): every peeked message is read via
    /// <c>BasicGet(autoAck:false)</c> and then <c>BasicNack(requeue:true)</c>'d back onto the queue, so
    /// peeking never removes anything -- it only sets the AMQP "redelivered" flag on those messages and,
    /// since RabbitMQ re-enqueues a nacked message at the back, may reorder the queue.
    /// </summary>
    /// <exception cref="BrokerUnavailableException">The broker cannot be reached.</exception>
    Task<DeadLetterPeekResult> PeekAsync(int limit, CancellationToken cancellationToken);

    /// <summary>Moves every message matching <paramref name="messageIds"/> (or all of them, if null/empty)
    /// back onto the main exchange with their original type/headers (retry count reset). Returns how many
    /// were requeued.</summary>
    /// <exception cref="BrokerUnavailableException">The broker cannot be reached.</exception>
    Task<int> RequeueAsync(IReadOnlyList<string>? messageIds, CancellationToken cancellationToken);

    /// <summary>Removes the single message with this <c>MessageId</c> from the DLQ. Returns 1 if a
    /// message was removed, 0 if none matched.</summary>
    /// <exception cref="BrokerUnavailableException">The broker cannot be reached.</exception>
    Task<int> DeleteAsync(string messageId, CancellationToken cancellationToken);
}
