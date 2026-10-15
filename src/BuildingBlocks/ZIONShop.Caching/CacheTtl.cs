namespace ZIONShop.Caching;

internal static class CacheTtl
{
    /// <summary>Applies +/-10% random jitter so many keys set at once don't expire in the same instant (stampede on expiry).</summary>
    public static TimeSpan Jitter(TimeSpan ttl)
    {
        var ratio = 1 + ((Random.Shared.NextDouble() * 0.2) - 0.1);
        var millis = Math.Max(ttl.TotalMilliseconds * ratio, 0);
        return TimeSpan.FromMilliseconds(millis);
    }
}
