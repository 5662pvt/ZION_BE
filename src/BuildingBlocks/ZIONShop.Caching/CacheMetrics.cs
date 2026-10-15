using System.Diagnostics.Metrics;

namespace ZIONShop.Caching;

internal static class CacheMetrics
{
    private static readonly Meter Meter = new("ZIONShop.Caching");

    private static readonly Counter<long> HitsCounter = Meter.CreateCounter<long>("cache.hits");
    private static readonly Counter<long> MissesCounter = Meter.CreateCounter<long>("cache.misses");
    private static readonly Counter<long> ErrorsCounter = Meter.CreateCounter<long>("cache.errors");

    public static void RecordHit(string cacheName) => HitsCounter.Add(1, new KeyValuePair<string, object?>("cache.name", cacheName));

    public static void RecordMiss(string cacheName) => MissesCounter.Add(1, new KeyValuePair<string, object?>("cache.name", cacheName));

    public static void RecordError(string cacheName) => ErrorsCounter.Add(1, new KeyValuePair<string, object?>("cache.name", cacheName));

    /// <summary>
    /// "zion:products:detail:id:{id}" -> "products.detail"; "zion:categories:tree" -> "categories.tree".
    /// </summary>
    public static string DeriveCacheName(string key)
    {
        var segments = key.Split(':', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length switch
        {
            >= 3 => $"{segments[1]}.{segments[2]}",
            2 => segments[1],
            _ => key
        };
    }
}
