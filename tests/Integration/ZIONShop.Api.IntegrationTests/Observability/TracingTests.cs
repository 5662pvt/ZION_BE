using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Observability;

[Collection(ApiTestCollection.Name)]
public class TracingTests
{
    private readonly ApiFactory _factory;

    public TracingTests(ApiFactory factory) => _factory = factory;

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    /// <summary>
    /// End-to-end trace: HTTP request -> outbox publish -> OrderCreated subscribers (incl. Inventory's
    /// reservation handler) -> Npgsql, all tied together by one TraceId. The ActivityListener below is
    /// registered up front (ApiTestCollection shares one ApiFactory/host across the whole assembly, so the
    /// real OpenTelemetry SDK listeners may already be live from an earlier test) and listens to every
    /// source with AllDataAndRecorded so nothing started by this specific request is missed, regardless of
    /// which ActivitySources existed before this test ran.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_Should_ProduceOneConnectedTrace_Across_Http_Outbox_Consumer_And_Database()
    {
        var activities = new List<Activity>();
        var sync = new object();
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                lock (sync) activities.Add(activity);
            }
        };
        ActivitySource.AddActivityListener(listener);

        var admin = await AdminClientAsync();
        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "COD");
        // Order leaves PendingStock once the Inventory module's OrderCreated handler has reserved stock
        // and Orders has consumed the resulting InventoryReserved event -- both happen asynchronously,
        // off the outbox poll, after the HTTP response for PlaceOrder already came back.
        await WaitForOrderStatusAsync(customer, placed.Id, "Confirmed");

        var healthResponse = await customer.GetAsync("/health/live");
        healthResponse.EnsureSuccessStatusCode();

        List<Activity> snapshot;
        lock (sync) snapshot = activities.ToList();

        var placeOrderActivity = snapshot.SingleOrDefault(a =>
            a.Kind == ActivityKind.Server &&
            string.Equals(GetHttpMethod(a), "POST", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(GetTag(a, "http.route"), "api/v1/orders", StringComparison.Ordinal));
        placeOrderActivity.Should().NotBeNull("the AspNetCore instrumentation should record a server activity for POST /api/v1/orders");

        var traceId = placeOrderActivity!.TraceId;

        var publishActivities = snapshot.Where(a =>
            a.OperationName == "OrderCreatedIntegrationEvent publish" && a.TraceId == traceId).ToList();
        publishActivities.Should().NotBeEmpty("the outbox publisher should re-anchor the OrderCreated publish span to the request's trace via the persisted traceparent");

        // ApiTestCollection shares one ApiFactory across the whole assembly, and some other tests spin up
        // additional hosts via WithWebHostBuilder -- each runs its own OutboxPublisherService polling the
        // same Postgres database, so the same outbox row can legitimately be published more than once
        // (at-least-once delivery; the idempotent inbox dedups consumers). That means more than one
        // "OrderCreatedIntegrationEvent publish" activity can carry this trace's TraceId, so assert against
        // the set of publish span ids rather than a single activity.
        var publishSpanIds = publishActivities.Select(a => a.SpanId).ToHashSet();

        // The handler activity is started by InMemoryEventBus using the resolved handler's CLR type name
        // (ZIONShop.EventBus.Diagnostics.MessagingDiagnostics / InMemoryEventBus.PublishAsync); every
        // subscriber to OrderCreatedIntegrationEvent (Inventory, Cart, Payments) is registered through the
        // idempotent-inbox overload, so they all resolve to the same generic wrapper type name -- there is
        // no way to single out "the Inventory handler" by activity name alone. Order reaching "Confirmed"
        // above is already proof that Inventory's handler ran (it is the only module that reserves stock
        // and emits InventoryReserved, which Orders consumes to confirm); this assertion proves that run
        // happened as a child span of one of the publish activities, inside the same trace.
        snapshot.Should().Contain(a =>
                a.TraceId == traceId &&
                publishSpanIds.Contains(a.ParentSpanId) &&
                string.Equals(GetTag(a, "event.type"), "OrderCreatedIntegrationEvent", StringComparison.Ordinal),
            "an OrderCreated subscriber (Inventory's reservation handler among them) should run as a child of one of the outbox publish activities, inside the same trace");

        snapshot.Should().Contain(a => a.TraceId == traceId && a.Source.Name == "Npgsql",
            "placing/confirming the order issues Npgsql commands that should be traced under the same request");

        snapshot.Should().NotContain(a =>
                a.IsAllDataRequested &&
                (string.Equals(GetTag(a, "http.route"), "health/live", StringComparison.OrdinalIgnoreCase) ||
                 (GetTag(a, "http.target") ?? string.Empty).Contains("health/live", StringComparison.OrdinalIgnoreCase)),
            "the AspNetCore instrumentation Filter should exclude /health/live from full recording");
    }

    [Fact]
    public void TracerProvider_And_MeterProvider_Should_RegisterNoOtlpExporter_When_EndpointNotConfigured()
    {
        // The default test host never sets OTEL_EXPORTER_OTLP_ENDPOINT/Otel:Endpoint (and is in the
        // Testing environment, which never exports regardless), so AddZionObservability should have left
        // the exporter-specific pipeline stages unset -- the SDK's private "processor"/"reader" fields stay
        // null until AddOtlpExporter() assigns them; there is no public API to ask "was an exporter added?"
        // directly, so this reaches past it via reflection rather than guessing from external behaviour.
        var tracerProvider = _factory.Services.GetRequiredService<TracerProvider>();
        var processor = tracerProvider.GetType().GetField("processor", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(tracerProvider);
        processor.Should().BeNull("no OTLP trace exporter/processor should be registered when no endpoint is configured");

        var meterProvider = _factory.Services.GetRequiredService<MeterProvider>();
        var reader = meterProvider.GetType().GetField("reader", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(meterProvider);
        reader.Should().BeNull("no OTLP metric exporter/reader should be registered when no endpoint is configured");
    }

    private static string? GetTag(Activity activity, string name) => activity.GetTagItem(name) as string;

    private static string? GetHttpMethod(Activity activity) => GetTag(activity, "http.method") ?? GetTag(activity, "http.request.method");
}
