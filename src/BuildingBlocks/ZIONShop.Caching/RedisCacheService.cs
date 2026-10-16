using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace ZIONShop.Caching;

public class RedisCacheService : IDistributedCacheService
{
    private static readonly TimeSpan WarnLogInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RedisUnavailableWindow = TimeSpan.FromSeconds(5);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly object _gatesLock = new();
    private readonly Dictionary<string, SingleFlightGate> _singleFlightGates = new();
    private long _lastWarnLogTicks = long.MinValue;
    private long _lastRedisErrorTicks = long.MinValue;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (IsRedisUnavailable()) return default;

        try
        {
            var db = _redis.GetDatabase();
            var value = await db.StringGetAsync(key);
            if (value.IsNullOrEmpty) return default;
            return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            RecordRedisFailure("GET", key, ex);
            return default;
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        if (IsRedisUnavailable()) return;

        try
        {
            var db = _redis.GetDatabase();
            var payload = JsonSerializer.Serialize(value, JsonOptions);
            await db.StringSetAsync(key, payload, expiration ?? TimeSpan.FromMinutes(5));
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            RecordRedisFailure("SET", key, ex);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (IsRedisUnavailable()) return;

        try
        {
            await _redis.GetDatabase().KeyDeleteAsync(key);
        }
        catch (Exception ex) when (IsRedisFailure(ex))
        {
            RecordRedisFailure("DEL", key, ex);
        }
    }

    public async Task RemoveManyAsync(params string[] keys)
    {
        foreach (var key in keys)
        {
            await RemoveAsync(key);
        }
    }

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var cacheName = CacheMetrics.DeriveCacheName(key);

        var cached = await GetAsync<T>(key, cancellationToken);
        if (cached is not null)
        {
            CacheMetrics.RecordHit(cacheName);
            return cached;
        }

        if (IsRedisUnavailable())
        {
            // Redis is down: the per-key gate and the Set below would only add latency
            // (every concurrent caller would serialize behind the semaphore for nothing,
            // since none of them can read/write the cache anyway). Fail open per-caller.
            CacheMetrics.RecordMiss(cacheName);
            return await factory(cancellationToken);
        }

        var gate = AcquireGate(key);
        try
        {
            await gate.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            ReleaseGate(key, gate);
            throw;
        }

        try
        {
            cached = await GetAsync<T>(key, cancellationToken);
            if (cached is not null)
            {
                CacheMetrics.RecordHit(cacheName);
                return cached;
            }

            CacheMetrics.RecordMiss(cacheName);
            var value = await factory(cancellationToken);
            if (value is not null && !IsRedisUnavailable())
            {
                await SetAsync(key, value, CacheTtl.Jitter(ttl), cancellationToken);
            }
            return value;
        }
        finally
        {
            gate.Semaphore.Release();
            ReleaseGate(key, gate);
        }
    }

    public async Task<IAsyncDisposable?> AcquireLockAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var token = Guid.NewGuid().ToString("N");
        var ok = await db.StringSetAsync($"lock:{resource}", token, ttl, When.NotExists);
        if (!ok)
        {
            _logger.LogDebug("Lock {Resource} already held", resource);
            return null;
        }
        return new RedisLock(db, resource, token);
    }

    private static bool IsRedisFailure(Exception ex) => ex is RedisException or ObjectDisposedException or TimeoutException;

    /// <summary>Redis is treated as unavailable while the multiplexer reports disconnected, or an
    /// operation failed within the last <see cref="RedisUnavailableWindow"/> -- whichever fires first
    /// lets <see cref="GetOrCreateAsync{T}"/> bypass the single-flight gate and skip the Set so a Redis
    /// outage degrades to one DB hit per caller instead of N serialized ones.</summary>
    private bool IsRedisUnavailable()
    {
        if (!_redis.IsConnected) return true;

        var lastError = Interlocked.Read(ref _lastRedisErrorTicks);
        if (lastError == long.MinValue) return false;
        return Environment.TickCount64 - lastError < RedisUnavailableWindow.TotalMilliseconds;
    }

    private void RecordRedisFailure(string operation, string key, Exception ex)
    {
        Interlocked.Exchange(ref _lastRedisErrorTicks, Environment.TickCount64);
        CacheMetrics.RecordError(CacheMetrics.DeriveCacheName(key));

        var now = Environment.TickCount64;
        var last = Interlocked.Read(ref _lastWarnLogTicks);
        if (now - last < WarnLogInterval.TotalMilliseconds) return;
        if (Interlocked.CompareExchange(ref _lastWarnLogTicks, now, last) != last) return;

        _logger.LogWarning(ex, "Redis cache {Operation} failed for key {Key}; failing open", operation, key);
    }

    /// <summary>Ref-counted so a gate is never removed from <see cref="_singleFlightGates"/> while another
    /// caller still holds or is waiting on it -- removing it early would let a late arrival on the same key
    /// create a second, independent gate and defeat single-flight (duplicate factory calls under contention).</summary>
    private sealed class SingleFlightGate
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int RefCount;
    }

    private SingleFlightGate AcquireGate(string key)
    {
        lock (_gatesLock)
        {
            if (!_singleFlightGates.TryGetValue(key, out var gate))
            {
                gate = new SingleFlightGate();
                _singleFlightGates[key] = gate;
            }
            gate.RefCount++;
            return gate;
        }
    }

    private void ReleaseGate(string key, SingleFlightGate gate)
    {
        lock (_gatesLock)
        {
            gate.RefCount--;
            if (gate.RefCount == 0 && _singleFlightGates.TryGetValue(key, out var current) && ReferenceEquals(current, gate))
            {
                _singleFlightGates.Remove(key);
            }
        }
    }

    private sealed class RedisLock : IAsyncDisposable
    {
        private readonly IDatabase _db;
        private readonly string _resource;
        private readonly string _token;

        public RedisLock(IDatabase db, string resource, string token)
        {
            _db = db;
            _resource = resource;
            _token = token;
        }

        public async ValueTask DisposeAsync()
        {
            var current = await _db.StringGetAsync($"lock:{_resource}");
            if (current == _token)
            {
                await _db.KeyDeleteAsync($"lock:{_resource}");
            }
        }
    }
}
