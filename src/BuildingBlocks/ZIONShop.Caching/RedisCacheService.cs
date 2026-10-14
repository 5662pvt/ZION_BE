using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Text.Json;

namespace ZIONShop.Caching;

public class RedisCacheService : IDistributedCacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var value = await db.StringGetAsync(key);
        if (value.IsNullOrEmpty) return default;
        return JsonSerializer.Deserialize<T>((string)value!, JsonOptions);
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var db = _redis.GetDatabase();
        var payload = JsonSerializer.Serialize(value, JsonOptions);
        await db.StringSetAsync(key, payload, expiration ?? TimeSpan.FromMinutes(5));
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await _redis.GetDatabase().KeyDeleteAsync(key);
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
