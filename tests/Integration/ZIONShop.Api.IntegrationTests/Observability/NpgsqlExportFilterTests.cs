using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Npgsql;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ZIONShop.Api.Extensions;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Logging.Resource;

namespace ZIONShop.Api.IntegrationTests.Observability;

/// <summary>
/// Builds the exact same trace pipeline production does
/// (<see cref="ObservabilityExtensions.ConfigureTracePipeline"/>) but with an InMemoryExporter instead
/// of OTLP, so these tests catch pipeline-wiring regressions -- like the unknown_service resource bug
/// from wrapping the OTLP exporter's own processor inside another processor -- not just the drop
/// predicate in isolation.
/// </summary>
[Collection(ApiTestCollection.Name)]
public class NpgsqlExportFilterTests
{
    private const double FullSamplingRatio = 1.0;

    private static readonly ActivitySource ParentSource = new("ZIONShop.Api.IntegrationTests.NpgsqlExportFilterTests");

    private readonly ApiFactory _factory;

    public NpgsqlExportFilterTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task OnEnd_Should_DropExportedNpgsqlActivity_When_CommandHasNoParent()
    {
        var exportedItems = new List<Activity>();
        using var provider = BuildProviderExportingTo(exportedItems);

        await RunSelectOneAsync();

        exportedItems.Should().BeEmpty(
            "a root (parentless) Npgsql command activity is background-poll noise and should be dropped at export time");
    }

    [Fact]
    public async Task OnEnd_Should_ExportNpgsqlActivity_When_CommandHasAParent()
    {
        var exportedItems = new List<Activity>();
        using var provider = BuildProviderExportingTo(exportedItems);

        using (ParentSource.StartActivity("parent-operation"))
        {
            await RunSelectOneAsync();
        }

        exportedItems.Should().Contain(a => a.Source.Name == "Npgsql",
            "a Npgsql command activity that has a parent should flow through the filter unchanged");
    }

    [Fact]
    public async Task ExportedActivity_Should_CarryTheRealServiceResource()
    {
        var resourceExporter = new ResourceCapturingExporter();
        using var provider = CreateProductionLikeBuilder()
            .ConfigureTracePipeline(FullSamplingRatio, otlpEndpoint: null, addExporter: tracing =>
                tracing.AddProcessor(new SimpleActivityExportProcessor(resourceExporter)))
            .Build();

        using (ParentSource.StartActivity("parent-operation"))
        {
            await RunSelectOneAsync();
        }

        // Regression guard: wrapping the exporter's processor inside a filtering decorator meant the
        // SDK only ever set ParentProvider (which carries the Resource) on the outer wrapper, so
        // every exported span showed up under service.name "unknown_service" in the Aspire Dashboard.
        resourceExporter.CapturedResource.Should().NotBeNull();
        resourceExporter.CapturedResource!.Attributes.Should().Contain(
            a => a.Key == "service.name" && (string)a.Value == ZionResourceAttributes.ServiceName);
    }

    [Fact]
    public async Task HttpClientInstrumentation_Should_NotRecord_Request_To_ConfiguredOtlpEndpoint()
    {
        var otlpPort = GetUnusedLoopbackPort();
        var otherPort = GetUnusedLoopbackPort();
        var otlpEndpoint = new Uri($"http://127.0.0.1:{otlpPort}/v1/traces");

        var exportedItems = new List<Activity>();
        using var provider = BuildProviderExportingTo(exportedItems, otlpEndpoint);

        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        await TryGetAsync(client, otlpEndpoint);
        await TryGetAsync(client, new Uri($"http://127.0.0.1:{otherPort}/"));

        // Scoped to the HttpClient source: the shared ApiFactory host's own outbox-publisher
        // background job keeps polling Postgres for the lifetime of the test run and lands on the
        // same process-wide "Npgsql" ActivitySource this provider also listens to via AddNpgsql() --
        // unrelated to what this test is about, but a real risk during a multi-second HTTP timeout.
        exportedItems.Where(a => a.Source.Name == "System.Net.Http").Should().ContainSingle(
            "the request to the configured OTLP endpoint must be filtered out while the other request is still recorded");
    }

    private static TracerProviderBuilder CreateProductionLikeBuilder() =>
        Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(resource => resource.AddService(
                ZionResourceAttributes.ServiceName,
                serviceVersion: ZionResourceAttributes.ServiceVersion,
                serviceInstanceId: ZionResourceAttributes.ServiceInstanceId))
            .AddSource(ParentSource.Name);

    private static TracerProvider BuildProviderExportingTo(ICollection<Activity> exportedItems, Uri? otlpEndpoint = null) =>
        CreateProductionLikeBuilder()
            .ConfigureTracePipeline(FullSamplingRatio, otlpEndpoint, addExporter: tracing =>
                tracing.AddProcessor(new SimpleActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems))))
            .Build();

    private async Task RunSelectOneAsync()
    {
        // ApiFactory only sets ConnectionStrings__DefaultConnection (from its Postgres Testcontainer)
        // once the host is actually built, which happens lazily -- force that via CreateClient(), same
        // as every other test in this collection, before reading the env var it sets.
        using var client = _factory.CreateClient();
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? throw new InvalidOperationException("ApiFactory did not set ConnectionStrings__DefaultConnection.");

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync();
    }

    private static async Task TryGetAsync(HttpClient client, Uri uri)
    {
        try
        {
            await client.GetAsync(uri);
        }
        catch (HttpRequestException)
        {
            // Nothing is listening on either loopback port -- the point is the Activity that the
            // BCL's HttpClient diagnostics create before the connection attempt, not a response.
        }
        catch (TaskCanceledException)
        {
        }
    }

    private static int GetUnusedLoopbackPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class ResourceCapturingExporter : BaseExporter<Activity>
    {
        public Resource? CapturedResource { get; private set; }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            CapturedResource ??= ParentProvider?.GetResource();
            return ExportResult.Success;
        }
    }

    public class IsRootNpgsqlActivityTests
    {
        [Fact]
        public void IsRootNpgsqlActivity_Should_ReturnTrue_When_SourceIsNpgsqlAndHasNoParent()
        {
            using var source = new ActivitySource("Npgsql");
            using var listener = ListenToEverything();

            using var activity = source.StartActivity("SELECT 1", ActivityKind.Client);

            NpgsqlRootDropProcessor.IsRootNpgsqlActivity(activity!).Should().BeTrue();
        }

        [Fact]
        public void IsRootNpgsqlActivity_Should_ReturnFalse_When_SourceIsNpgsqlButHasAParent()
        {
            using var parentSource = new ActivitySource("ZIONShop.Api.IntegrationTests.NpgsqlExportFilterTests.Parent");
            using var npgsqlSource = new ActivitySource("Npgsql");
            using var listener = ListenToEverything();

            using var parent = parentSource.StartActivity("parent-operation");
            using var child = npgsqlSource.StartActivity("SELECT 1", ActivityKind.Client);

            NpgsqlRootDropProcessor.IsRootNpgsqlActivity(child!).Should().BeFalse();
        }

        [Fact]
        public void IsRootNpgsqlActivity_Should_ReturnFalse_When_RootActivityIsNotNpgsql()
        {
            using var source = new ActivitySource("ZIONShop.Api.IntegrationTests.NpgsqlExportFilterTests.NotNpgsql");
            using var listener = ListenToEverything();

            using var activity = source.StartActivity("POST api/v1/orders", ActivityKind.Server);

            NpgsqlRootDropProcessor.IsRootNpgsqlActivity(activity!).Should().BeFalse();
        }

        private static ActivityListener ListenToEverything()
        {
            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }
    }

    public class IsOtlpExportRequestTests
    {
        private static readonly Uri ConfiguredEndpoint = new("http://otel-collector:4317/v1/traces");

        [Fact]
        public void IsOtlpExportRequest_Should_ReturnTrue_When_HostAndPortMatchConfiguredEndpoint()
        {
            var request = new Uri("http://otel-collector:4317/opentelemetry.proto.collector.trace.v1.TraceService/Export");

            ObservabilityExtensions.IsOtlpExportRequest(request, ConfiguredEndpoint).Should().BeTrue();
        }

        [Fact]
        public void IsOtlpExportRequest_Should_ReturnTrue_When_PathIsAWellKnownOtlpPath_EvenOnADifferentHost()
        {
            var request = new Uri("http://127.0.0.1:4318/v1/traces");

            ObservabilityExtensions.IsOtlpExportRequest(request, ConfiguredEndpoint).Should().BeTrue();
        }

        [Fact]
        public void IsOtlpExportRequest_Should_ReturnFalse_When_NeitherHostPortNorPathMatch()
        {
            var request = new Uri("https://api.example.com/v1/products");

            ObservabilityExtensions.IsOtlpExportRequest(request, ConfiguredEndpoint).Should().BeFalse();
        }

        [Fact]
        public void IsOtlpExportRequest_Should_ReturnFalse_When_RequestUriIsNull()
        {
            ObservabilityExtensions.IsOtlpExportRequest(null, ConfiguredEndpoint).Should().BeFalse();
        }
    }
}
