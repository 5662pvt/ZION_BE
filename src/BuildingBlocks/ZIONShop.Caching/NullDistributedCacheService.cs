namespace ZIONShop.Caching;

/// <summary>Always-miss cache used when Redis is not configured/available (e.g. the Testing environment).</summary>
public sealed class NullDistributedCacheService : IDistributedCacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) => Task.FromResult<T?>(default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveManyAsync(params string[] keys) => Task.CompletedTask;

    public Task<IAsyncDisposable?> AcquireLockAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default)
        => Task.FromResult<IAsyncDisposable?>(null);

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        CacheMetrics.RecordMiss(CacheMetrics.DeriveCacheName(key));
        return await factory(cancellationToken);
    }
}
