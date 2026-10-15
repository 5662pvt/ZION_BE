using System.Text;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using ZIONShop.EventBus.Admin;
using ZIONShop.EventBus.Diagnostics;

namespace ZIONShop.EventBus.RabbitMq;

public sealed class RabbitMqDeadLetterAdmin : IDeadLetterAdmin
{
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqPublisher _publisher;
    private readonly RabbitMqOptions _options;

    public RabbitMqDeadLetterAdmin(RabbitMqConnection connection, RabbitMqPublisher publisher, IOptions<RabbitMqOptions> options)
    {
        _connection = connection;
        _publisher = publisher;
        _options = options.Value;
    }

    public Task<DeadLetterPeekResult> PeekAsync(int limit, CancellationToken cancellationToken)
    {
        limit = Math.Clamp(limit, 1, 100);
        var queueName = DlqName;

        using var channel = OpenChannel();
        if (!TryDeclarePassive(channel, queueName, out var depth))
            return Task.FromResult(new DeadLetterPeekResult(queueName, 0, Array.Empty<DeadLetterMessage>()));

        var fetched = new List<BasicGetResult>(Math.Min(limit, (int)Math.Min(depth, int.MaxValue)));
        for (var i = 0; i < limit; i++)
        {
            var result = channel.BasicGet(queueName, autoAck: false);
            if (result is null) break;
            fetched.Add(result);
        }

        var messages = fetched.Select(ToDeadLetterMessage).ToList();

        // Peeking must never remove anything from the queue -- nack every fetched delivery back with
        // requeue:true. RabbitMQ re-enqueues a nacked message at the back, so this may reorder the
        // queue and always sets the AMQP "redelivered" flag on these messages (documented on
        // IDeadLetterAdmin.PeekAsync).
        foreach (var result in fetched)
            channel.BasicNack(result.DeliveryTag, multiple: false, requeue: true);

        return Task.FromResult(new DeadLetterPeekResult(queueName, depth, messages));
    }

    public async Task<int> RequeueAsync(IReadOnlyList<string>? messageIds, CancellationToken cancellationToken)
    {
        var queueName = DlqName;
        using var channel = OpenChannel();
        if (!TryDeclarePassive(channel, queueName, out var depth))
            return 0;

        var matchAll = messageIds is null || messageIds.Count == 0;
        var pending = matchAll ? null : new HashSet<string>(messageIds!, StringComparer.Ordinal);

        var requeued = 0;
        var toRequeue = new List<ulong>();
        for (var i = 0; i < depth; i++)
        {
            var result = channel.BasicGet(queueName, autoAck: false);
            if (result is null) break;

            var messageId = result.BasicProperties?.MessageId;
            var shouldRequeue = matchAll || (messageId is not null && pending!.Remove(messageId));
            if (!shouldRequeue)
            {
                toRequeue.Add(result.DeliveryTag);
                continue;
            }

            var typeName = result.BasicProperties?.Type ?? "Unknown";
            await _publisher.PublishRequeuedAsync(
                messageId ?? Guid.NewGuid().ToString(), typeName, result.Body.ToArray(),
                result.BasicProperties?.Headers, cancellationToken);
            channel.BasicAck(result.DeliveryTag, multiple: false);
            requeued++;
        }

        foreach (var tag in toRequeue)
            channel.BasicNack(tag, multiple: false, requeue: true);

        return requeued;
    }

    public Task<int> DeleteAsync(string messageId, CancellationToken cancellationToken)
    {
        var queueName = DlqName;
        using var channel = OpenChannel();
        if (!TryDeclarePassive(channel, queueName, out var depth))
            return Task.FromResult(0);

        var deleted = 0;
        var toRequeue = new List<ulong>();
        for (var i = 0; i < depth; i++)
        {
            var result = channel.BasicGet(queueName, autoAck: false);
            if (result is null) break;

            if (deleted == 0 && string.Equals(result.BasicProperties?.MessageId, messageId, StringComparison.Ordinal))
            {
                channel.BasicAck(result.DeliveryTag, multiple: false);
                deleted = 1;
            }
            else
            {
                toRequeue.Add(result.DeliveryTag);
            }
        }

        foreach (var tag in toRequeue)
            channel.BasicNack(tag, multiple: false, requeue: true);

        return Task.FromResult(deleted);
    }

    private string DlqName => RabbitMqTopology.DeadLetterQueueName(_options.QueueName);

    private IModel OpenChannel()
    {
        if (!_connection.TryGetConnection(out var connection))
            throw new BrokerUnavailableException("RabbitMQ broker is unavailable.");
        return connection.CreateModel();
    }

    /// <summary>False (queue not declared yet -- nothing was ever dead-lettered) is a normal outcome,
    /// not a broker-unavailable failure; any other exception while doing a passive declare still means
    /// the broker/channel is unusable.</summary>
    private static bool TryDeclarePassive(IModel channel, string queueName, out uint messageCount)
    {
        try
        {
            messageCount = channel.QueueDeclarePassive(queueName).MessageCount;
            return true;
        }
        catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 404)
        {
            messageCount = 0;
            return false;
        }
    }

    private static DeadLetterMessage ToDeadLetterMessage(BasicGetResult result)
    {
        var headers = result.BasicProperties?.Headers;
        var (reason, deathCount, firstDeathAt) = XDeathHeader.Read(headers);
        var lastError = MessagingDiagnostics.ExtractHeaderString(headers, "x-last-error");
        var payload = Encoding.UTF8.GetString(result.Body.Span);

        return new DeadLetterMessage(
            result.BasicProperties?.MessageId,
            result.BasicProperties?.Type ?? "Unknown",
            reason,
            deathCount,
            firstDeathAt,
            lastError,
            PayloadMasker.MaskJson(payload));
    }

    /// <summary>Parses the AMQP "x-death" header array RabbitMQ attaches every time a message is
    /// dead-lettered. Entries are ordered most-recent-death-first, so the first entry's "reason"/"count"
    /// describe the latest dead-lettering and the last entry's "time" is the very first one.</summary>
    private static class XDeathHeader
    {
        public static (string Reason, int DeathCount, DateTimeOffset? FirstDeathAt) Read(IDictionary<string, object>? headers)
        {
            if (headers is null || !headers.TryGetValue("x-death", out var raw) || raw is not IList<object> deaths || deaths.Count == 0)
                return ("unknown", 0, null);

            var mostRecent = deaths[0] as IDictionary<string, object>;
            var oldest = deaths[^1] as IDictionary<string, object>;

            var reason = mostRecent is not null && mostRecent.TryGetValue("reason", out var r) ? DecodeString(r) : "unknown";
            var count = mostRecent is not null && mostRecent.TryGetValue("count", out var c) ? DecodeInt(c) : deaths.Count;
            DateTimeOffset? firstDeathAt = oldest is not null && oldest.TryGetValue("time", out var t) && t is AmqpTimestamp timestamp
                ? DateTimeOffset.FromUnixTimeSeconds(timestamp.UnixTime)
                : null;

            return (reason, count, firstDeathAt);
        }

        private static string DecodeString(object value) => value switch
        {
            string s => s,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => value.ToString() ?? "unknown"
        };

        private static int DecodeInt(object value) => value switch
        {
            int i => i,
            long l => (int)l,
            _ => 0
        };
    }
}
