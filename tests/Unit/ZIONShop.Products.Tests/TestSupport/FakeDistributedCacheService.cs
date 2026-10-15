using ZIONShop.Caching;

namespace ZIONShop.Products.Tests.TestSupport;

internal sealed class FakeDistributedCacheService : IDistributedCacheService
{
    private readonly Dictionary<string, object?> _store = new();

    public bool Contains(string key) => _store.ContainsKey(key);

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.TryGetValue(key, out var value) && value is T typed ? typed : default);

    public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task RemoveManyAsync(params string[] keys)
    {
        foreach (var key in keys) _store.Remove(key);
        return Task.CompletedTask;
    }

    public Task<IAsyncDisposable?> AcquireLockAsync(string resource, TimeSpan ttl, CancellationToken cancellationToken = default) =>
        Task.FromResult<IAsyncDisposable?>(null);

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T?>> factory, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var cached) && cached is T typedCached) return typedCached;

        var value = await factory(cancellationToken);
        if (value is not null) _store[key] = value;
        return value;
    }
}
