using ZIONShop.EventBus.RabbitMq;

namespace ZIONShop.EventBus.InMemory;

/// <summary>
/// There is no broker (and so no DLQ) under the InMemory provider -- every operation reports an
/// empty queue named "in-memory" instead of throwing: peeking finds nothing to show, and the
/// mutating operations (requeue/delete) report "nothing matched" (0) rather than a conflict, since
/// from the caller's point of view the DLQ genuinely has no messages to act on.
/// </summary>
public sealed class InMemoryDeadLetterAdmin : IDeadLetterAdmin
{
    private const string QueueName = "in-memory";

    public Task<DeadLetterPeekResult> PeekAsync(int limit, CancellationToken cancellationToken) =>
        Task.FromResult(new DeadLetterPeekResult(QueueName, 0, Array.Empty<DeadLetterMessage>()));

    public Task<int> RequeueAsync(IReadOnlyList<string>? messageIds, CancellationToken cancellationToken) =>
        Task.FromResult(0);

    public Task<int> DeleteAsync(string messageId, CancellationToken cancellationToken) =>
        Task.FromResult(0);
}
