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
    public int RetryCount { get; set; } = 5;
}
