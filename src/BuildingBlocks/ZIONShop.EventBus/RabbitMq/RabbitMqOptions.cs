namespace ZIONShop.EventBus.RabbitMq;

public class RabbitMqOptions
{
    public const string SectionName = "EventBus";
    public string Provider { get; set; } = "InMemory";
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string User { get; set; } = "guest";
    public string Pass { get; set; } = "guest";
    public string Exchange { get; set; } = "zionshop.events";
    public string QueueName { get; set; } = "zionshop.api";

    /// <summary>Number of delayed retries (via <c>{QueueName}.retry.N</c> queues) before a message is dead-lettered.</summary>
    public int RetryCount { get; set; } = 5;
    public int OutboxPollSeconds { get; set; } = 2;

    /// <summary>Consumer prefetch (QoS) -- caps in-flight unacked deliveries per channel.</summary>
    public int PrefetchCount { get; set; } = 20;

    /// <summary>How long a publish waits for the broker's publisher confirm before it is treated as failed.</summary>
    public int ConfirmTimeoutSeconds { get; set; } = 5;

    /// <summary>Delay (seconds) for retry attempt N; attempts beyond the array length reuse the last value.</summary>
    public int[] RetryDelaysSeconds { get; set; } = { 1, 5, 15, 30, 60 };
}
