namespace ZIONShop.Caching;

public interface IDistributedCacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default);
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
    Task<IAsyncDisposable?> AcquireLockAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default);
}
