using System.Diagnostics.Metrics;

namespace ZIONShop.Caching.IntegrationTests.Infrastructure;

/// <summary>Counts "ZIONShop.Caching" instrument measurements for the lifetime of the listener.</summary>
public sealed class MetricsCollector : IDisposable
{
    private readonly MeterListener _listener = new();

    public long Hits { get; private set; }
    public long Misses { get; private set; }
    public long Errors { get; private set; }

    public MetricsCollector()
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == "ZIONShop.Caching") listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, measurement, _, _) =>
        {
            switch (instrument.Name)
            {
                case "cache.hits": Hits += measurement; break;
                case "cache.misses": Misses += measurement; break;
                case "cache.errors": Errors += measurement; break;
            }
        });
        _listener.Start();
    }

    public void Dispose() => _listener.Dispose();
}
