namespace ZIONShop.Orders.Application.Interfaces;

/// <summary>
/// Serializes concurrent PlaceOrder requests that share the same (userId, Idempotency-Key) so the whole
/// handler -- including the existing-key replay check and every side effect it may perform (coupon/gift
/// reservation, order insert) -- runs for one request at a time before the next one even looks at the key.
/// </summary>
public interface IIdempotencyLock
{
    /// <summary>
    /// Attempts to acquire the lock for (<paramref name="userId"/>, <paramref name="key"/>), waiting for other
    /// holders to release it. Returns <c>null</c> if the lock could not be acquired before the internal timeout
    /// elapsed (an earlier request for the same key is taking too long), otherwise an <see cref="IAsyncDisposable"/>
    /// that releases the lock when disposed.
    /// </summary>
    Task<IAsyncDisposable?> AcquireAsync(Guid userId, string key, CancellationToken cancellationToken = default);
}
