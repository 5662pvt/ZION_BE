using System.Diagnostics;
using OpenTelemetry;

namespace ZIONShop.Api.Extensions;

/// <summary>
/// Clears the Recorded flag on root (parentless) Npgsql command activities in OnEnd. Npgsql's own
/// ActivitySource ("Npgsql") emits one of these per query, including every background poll (outbox
/// publishers, outbox/queue gauges, the cleanup job) that isn't already inside an HTTP/messaging span
/// -- flooding the trace list with single-span "ZionShop" traces. A Sampler can't key off this:
/// Npgsql tags its command activities (db.system/db.system.name) via SetTag AFTER the sampling
/// decision is made, and SamplingParameters never exposes the originating ActivitySource either way,
/// so OnEnd (where the Activity is fully tagged and sourced) is the first reliable point to drop it.
/// Must be registered via AddProcessor() BEFORE the exporter's own processor -- clearing Recorded
/// here, rather than wrapping the exporter's processor in a filtering decorator, keeps the exporter
/// directly in the SDK's top-level processor list so the SDK still sets its ParentProvider (and thus
/// the Resource) on it; wrapping it previously left the exporter with no ParentProvider at all, which
/// showed up as every span exporting under service.name "unknown_service".
/// </summary>
public sealed class NpgsqlRootDropProcessor : BaseProcessor<Activity>
{
    private const string NpgsqlSourceName = "Npgsql";

    public override void OnEnd(Activity data)
    {
        if (IsRootNpgsqlActivity(data))
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
    }

    public static bool IsRootNpgsqlActivity(Activity activity) =>
        activity.Source.Name == NpgsqlSourceName && activity.ParentSpanId == default;
}
