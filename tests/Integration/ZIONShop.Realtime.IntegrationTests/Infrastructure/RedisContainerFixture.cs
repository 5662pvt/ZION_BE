using StackExchange.Redis;
using Testcontainers.Redis;

namespace ZIONShop.Realtime.IntegrationTests.Infrastructure;

/// <summary>Same convention as <c>ZIONShop.Caching.IntegrationTests</c>'s fixture of the same name -- a shared Redis container per collection, restored in a finally block by any test that stops it.</summary>
public sealed class RedisContainerFixture : IAsyncLifetime
{
    public RedisContainer Redis { get; } = new RedisBuilder().WithImage("redis:7-alpine").Build();

    public Task InitializeAsync() => Redis.StartAsync();

    public Task DisposeAsync() => Redis.DisposeAsync().AsTask();

    public IConnectionMultiplexer Connect(int timeoutMs = 2000)
    {
        var options = ConfigurationOptions.Parse(Redis.GetConnectionString(), true);
        options.AbortOnConnectFail = false;
        options.ConnectTimeout = timeoutMs;
        options.SyncTimeout = timeoutMs;
        options.AsyncTimeout = timeoutMs;
        return ConnectionMultiplexer.Connect(options);
    }
}

[CollectionDefinition(Name)]
public sealed class RedisTestCollection : ICollectionFixture<RedisContainerFixture>
{
    public const string Name = "Realtime Redis container";
}
