namespace ZIONShop.Caching;

public interface IDistributedCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task RemoveManyAsync(params string[] keys);
    Task<IAsyncDisposable?> AcquireLockAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache-aside with in-process single-flight: on a miss, only one caller per key runs
    /// <paramref name="factory"/> while others wait and reuse its result. Only non-null values are cached.
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan ttl, CancellationToken cancellationToken = default);
}
