using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using ZIONShop.EventBus.Registry;

namespace ZIONShop.EventBus.RabbitMq;

/// <summary>
/// Idempotent topology declaration shared by the publisher and the consumer, so both always agree
/// on exchange/queue names and arguments:
///
///   {Exchange} (topic)  --bind per registered event type-->  {QueueName}
///   {QueueName}            --nack(requeue:false) / x-dead-letter-*-->  {Exchange}.dlx (direct) --> {QueueName}.dlq
///   {QueueName}.retry.N    --x-message-ttl expiry / x-dead-letter-*-->  default exchange "" --> {QueueName}
/// </summary>
public static class RabbitMqTopology
{
    private const string DeadLetterExchangeArg = "x-dead-letter-exchange";
    private const string DeadLetterRoutingKeyArg = "x-dead-letter-routing-key";
    private const string MessageTtlArg = "x-message-ttl";

    public static string DeadLetterExchangeName(string exchange) => $"{exchange}.dlx";

    public static string DeadLetterQueueName(string queueName) => $"{queueName}.dlq";

    public static string RetryQueueName(string queueName, int attempt) => $"{queueName}.retry.{attempt}";

    /// <summary>Delay for retry attempt N (1-based); attempts beyond the configured array reuse the last value.</summary>
    public static int DelaySecondsForAttempt(int[] retryDelaysSeconds, int attempt)
    {
        if (retryDelaysSeconds is not { Length: > 0 }) return 60;
        var index = Math.Min(Math.Max(attempt, 1) - 1, retryDelaysSeconds.Length - 1);
        return retryDelaysSeconds[index];
    }

    /// <summary>
    /// Declares the exchange/DLX/main queue/DLQ/retry queues and binds the main queue for every
    /// registered event type. May return a <em>different</em> <see cref="IModel"/> than the one
    /// passed in -- see <see cref="DeclareQueueWithMigration"/> -- callers must use the returned
    /// channel for everything afterward.
    /// </summary>
    public static IModel Declare(IConnection connection, IModel channel, RabbitMqOptions options,
        IntegrationEventTypeRegistry registry, ILogger logger)
    {
        var dlxName = DeadLetterExchangeName(options.Exchange);

        channel.ExchangeDeclare(options.Exchange, ExchangeType.Topic, durable: true);
        channel.ExchangeDeclare(dlxName, ExchangeType.Direct, durable: true);

        var mainQueueArgs = new Dictionary<string, object>
        {
            [DeadLetterExchangeArg] = dlxName,
            [DeadLetterRoutingKeyArg] = options.QueueName
        };
        channel = DeclareQueueWithMigration(connection, channel, options.QueueName, mainQueueArgs, logger);

        var dlqName = DeadLetterQueueName(options.QueueName);
        channel = DeclareQueueWithMigration(connection, channel, dlqName, args: null, logger);
        channel.QueueBind(dlqName, dlxName, options.QueueName);

        for (var attempt = 1; attempt <= options.RetryCount; attempt++)
        {
            var retryQueueName = RetryQueueName(options.QueueName, attempt);
            var ttlMs = (long)DelaySecondsForAttempt(options.RetryDelaysSeconds, attempt) * 1000;
            var retryArgs = new Dictionary<string, object>
            {
                [MessageTtlArg] = ttlMs,
                [DeadLetterExchangeArg] = string.Empty,
                [DeadLetterRoutingKeyArg] = options.QueueName
            };
            channel = DeclareQueueWithMigration(connection, channel, retryQueueName, retryArgs, logger);
        }

        foreach (var eventType in registry.RegisteredTypes)
            channel.QueueBind(options.QueueName, options.Exchange, eventType.Name);

        return channel;
    }

    /// <summary>
    /// Environments that predate this topology already have <c>{QueueName}</c> declared with no
    /// arguments; redeclaring it with dead-letter args triggers 406 PRECONDITION_FAILED, which
    /// closes the channel. Recover with a fresh channel: delete the (expected-empty) queue and
    /// redeclare with the correct arguments. A non-empty queue means someone needs to drain it by
    /// hand first -- fail loudly rather than silently dropping messages.
    /// </summary>
    private static IModel DeclareQueueWithMigration(IConnection connection, IModel channel, string queueName,
        IDictionary<string, object>? args, ILogger logger)
    {
        try
        {
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, args);
            return channel;
        }
        catch (OperationInterruptedException ex) when (ex.ShutdownReason?.ReplyCode == 406)
        {
            logger.LogWarning(
                "Queue '{Queue}' already exists without the expected arguments (406 PRECONDITION_FAILED); attempting a one-time migration",
                queueName);

            // The 406 already closed this channel on the broker side; dispose the now-unusable .NET
            // object instead of abandoning it.
            try { channel.Dispose(); } catch { /* already closed by the broker */ }

            var freshChannel = connection.CreateModel();
            try
            {
                freshChannel.QueueDelete(queueName, ifUnused: false, ifEmpty: true);
            }
            catch (OperationInterruptedException deleteEx)
            {
                throw new InvalidOperationException(
                    $"Queue '{queueName}' was created before dead-letter/retry arguments were added and still has " +
                    "messages in it. Drain or delete it once manually, then retry.", deleteEx);
            }

            freshChannel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, args);
            return freshChannel;
        }
    }
}
