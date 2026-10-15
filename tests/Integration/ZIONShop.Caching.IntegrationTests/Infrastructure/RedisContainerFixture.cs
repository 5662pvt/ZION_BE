using StackExchange.Redis;
using Testcontainers.Redis;

namespace ZIONShop.Caching.IntegrationTests.Infrastructure;

/// <summary>Shared Redis container for the whole collection -- tests that stop/start it (broker-outage
/// style) restore it in a finally block, same convention as the EventBus RabbitMq outage tests.</summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    public RedisContainer Redis { get; } = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public Task InitializeAsync() => Redis.StartAsync();

    public Task DisposeAsync() => Redis.DisposeAsync().AsTask();

    public IConnectionMultiplexer Connect() => ConnectionMultiplexer.Connect(BuildOptions());

    public ConfigurationOptions BuildOptions()
    {
        var options = ConfigurationOptions.Parse(Redis.GetConnectionString(), true);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = 2000;
        options.SyncTimeout = 2000;
        return options;
    }
}

[CollectionDefinition(Name)]
public sealed class RedisTestCollection : ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "Redis container";
}
