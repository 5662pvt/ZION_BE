using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using StackExchange.Redis;
using ZIONShop.Caching.IntegrationTests.Infrastructure;

namespace ZIONShop.Caching.IntegrationTests;

[Collection(RedisTestCollection.Name)]
public class RedisCacheServiceTests
{
    private readonly RedisContainerFixture _fixture;

    public RedisCacheServiceTests(RedisContainerFixture fixture) => _fixture = fixture;

    private (IConnectionMultiplexer multiplexer, RedisCacheService cache) CreateCache()
    {
        var multiplexer = _fixture.Connect();
        return (multiplexer, new RedisCacheService(multiplexer, NullLogger<RedisCacheService>.Instance));
    }

    [Fact]
    public async Task GetOrCreateAsync_Should_NotCallFactory_When_ValueAlreadyCached()
    {
        var (multiplexer, cache) = CreateCache();
        using var _ = multiplexer;
        var key = $"zion:test:hit:{Guid.NewGuid()}";
        var calls = 0;

        await cache.GetOrCreateAsync(key, _ => { calls++; return Task.FromResult<string?>("value"); }, TimeSpan.FromMinutes(5), CancellationToken.None);
        var second = await cache.GetOrCreateAsync(key, _ => { calls++; return Task.FromResult<string?>("value"); }, TimeSpan.FromMinutes(5), CancellationToken.None);

        calls.Should().Be(1);
        second.Should().Be("value");
    }

    [Fact]
    public async Task GetOrCreateAsync_Should_ReloadFromFactory_When_TtlExpires()
    {
        var (multiplexer, cache) = CreateCache();
        using var _ = multiplexer;
        var key = $"zion:test:ttl:{Guid.NewGuid()}";
        var calls = 0;

        await cache.GetOrCreateAsync(key, _ => { calls++; return Task.FromResult<string?>("v1"); }, TimeSpan.FromSeconds(1), CancellationToken.None);
        await Task.Delay(TimeSpan.FromSeconds(1.6));
        var afterExpiry = await cache.GetOrCreateAsync(key, _ => { calls++; return Task.FromResult<string?>("v2"); }, TimeSpan.FromSeconds(1), CancellationToken.None);

        calls.Should().Be(2);
        afterExpiry.Should().Be("v2");
    }

    [Fact]
    public async Task GetOrCreateAsync_Should_CallFactoryExactlyOnce_When_FiftyConcurrentCallsRaceOnColdKey()
    {
        var (multiplexer, cache) = CreateCache();
        using var _ = multiplexer;
        var key = $"zion:test:stampede:{Guid.NewGuid()}";
        var calls = 0;

        Task<string?> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            return Task.Delay(150, ct).ContinueWith(_ => (string?)"computed", ct);
        }

        var tasks = Enumerable.Range(0, 50).Select(_ => cache.GetOrCreateAsync(key, Factory, TimeSpan.FromMinutes(5), CancellationToken.None));
        var results = await Task.WhenAll(tasks);

        calls.Should().Be(1);
        results.Should().OnlyContain(r => r == "computed");
    }

    [Fact]
    public async Task GetOrCreateAsync_Should_FailOpenToFactory_When_RedisIsDown()
    {
        // CreateCache()'s default options carry the fixture's production-sized 2000ms ConnectTimeout/
        // SyncTimeout, which leaves barely any margin under the 3s assertion below once the stopped
        // container's down-detection eats into that same 2000ms budget -- intermittently timing out
        // under machine load. Use the same tight, deterministic timeouts as the contention test further
        // below so the down-detection budget (and therefore this assertion's margin) stays small and
        // reliable regardless of load.
        var options = _fixture.BuildOptions();
        options.ConnectTimeout = 300;
        options.SyncTimeout = 300;
        using var multiplexer = ConnectionMultiplexer.Connect(options);
        var cache = new RedisCacheService(multiplexer, NullLogger<RedisCacheService>.Instance);
        var key = $"zion:test:failopen:{Guid.NewGuid()}";

        await _fixture.Redis.StopAsync();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var act = async () => await cache.GetOrCreateAsync(key, _ => Task.FromResult<string?>("fallback"), TimeSpan.FromMinutes(5), CancellationToken.None);

            var value = await act.Should().NotThrowAsync();
            stopwatch.Stop();

            value.Subject.Should().Be("fallback");
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(2));
        }
        finally
        {
            await _fixture.Redis.StartAsync();
        }
    }

    [Fact]
    public async Task GetOrCreateAsync_Should_RunFactoriesConcurrently_When_RedisIsDownUnderContention()
    {
        // Tighter timeouts than CreateCache()'s default (2000ms): docker-stopping the container drops the
        // connection without a clean FIN/RST, so detection time is bounded by SyncTimeout. A production-sized
        // timeout would make Redis-down *detection* (not the bug under test) dominate the assertion below.
        var options = _fixture.BuildOptions();
        options.ConnectTimeout = 300;
        options.SyncTimeout = 300;
        var multiplexer = ConnectionMultiplexer.Connect(options);
        using var _ = multiplexer;
        var cache = new RedisCacheService(multiplexer, NullLogger<RedisCacheService>.Instance);
        var key = $"zion:test:failopen-contention:{Guid.NewGuid()}";
        var calls = 0;

        Task<string?> Factory(CancellationToken ct)
        {
            Interlocked.Increment(ref calls);
            return Task.Delay(200, ct).ContinueWith(_ => (string?)"fallback", ct);
        }

        await _fixture.Redis.StopAsync();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            var tasks = Enumerable.Range(0, 20).Select(_ => cache.GetOrCreateAsync(key, Factory, TimeSpan.FromMinutes(5), CancellationToken.None));
            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Single-flight is intentionally bypassed while Redis is down (see RedisCacheService), so every
            // caller runs the factory independently -- that's what keeps latency flat instead of serialized.
            results.Should().OnlyContain(r => r == "fallback");
            calls.Should().Be(20);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1.5));
        }
        finally
        {
            await _fixture.Redis.StartAsync();
        }
    }

    [Fact]
    public async Task RemoveAsync_Should_CauseSubsequentGetToMiss_When_KeyWasCached()
    {
        var (multiplexer, cache) = CreateCache();
        using var _ = multiplexer;
        var key = $"zion:test:remove:{Guid.NewGuid()}";

        await cache.SetAsync(key, "value", TimeSpan.FromMinutes(5));
        (await cache.GetAsync<string>(key)).Should().Be("value");

        await cache.RemoveAsync(key);

        (await cache.GetAsync<string>(key)).Should().BeNull();
    }

    [Fact]
    public async Task Metrics_Should_IncrementHitsAndMisses_When_CacheIsUsed()
    {
        var (multiplexer, cache) = CreateCache();
        using var _ = multiplexer;
        using var metrics = new MetricsCollector();
        var key = $"zion:test:metrics:detail:{Guid.NewGuid()}";

        await cache.GetOrCreateAsync(key, _ => Task.FromResult<string?>("value"), TimeSpan.FromMinutes(5), CancellationToken.None);
        await cache.GetOrCreateAsync(key, _ => Task.FromResult<string?>("value"), TimeSpan.FromMinutes(5), CancellationToken.None);

        metrics.Misses.Should().BeGreaterThanOrEqualTo(1);
        metrics.Hits.Should().BeGreaterThanOrEqualTo(1);
    }
}
