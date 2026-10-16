using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using ZIONShop.Contracts.Orders;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.Api.IntegrationTests.SystemOps;

/// <summary>
/// DEV decision (feature/w14 rework): the "worker" health check must never fail /health/ready --
/// nobody running the Worker (or the API with BackgroundProcessing:Enabled=false) is an operational
/// fact about the deployment, not a reason for a load balancer to pull this API instance out of
/// rotation. This builds its own host against real Postgres + RabbitMq containers (not the shared
/// ApiTestCollection fixture, which pins EventBus:Provider=InMemory process-wide for the rest of the
/// suite) with BackgroundProcessing disabled on this host too and no Worker started anywhere, so the
/// main queue genuinely has zero consumers -- the same shape as a real deployment where the API has
/// handed consumers to a Worker that was never started.
///
/// Tagged into <see cref="ApiTestCollection"/> (same as <c>BackgroundProcessingTests</c>) purely to
/// serialize against it: this still builds its own independent host, but it overwrites the process-wide
/// ConnectionStrings__DefaultConnection/EventBus__* environment variables for the duration of the test,
/// which would otherwise race any other collection's test reading those same variables (e.g.
/// NpgsqlExportFilterTests asserting on ApiFactory's own connection string) since xUnit runs separate
/// collections in parallel by default.
/// </summary>
[Collection(ApiTestCollection.Name)]
public sealed class WorkerReadinessIsolationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("zionshop_worker_readiness_test")
        .WithUsername("postgres")
        .WithPassword("postgres")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private readonly string _uploadsRoot = Path.Combine(Path.GetTempPath(), "zion-uploads-" + Guid.NewGuid().ToString("N"));

    private WebApplicationFactory<Program>? _factory;
    private EnvironmentVariableScope? _envScope;

    private record HealthCheckDto(string Name, string Status, double DurationMs, string? Description);
    private record HealthReportDto(string Status, double TotalDurationMs, List<HealthCheckDto> Checks);

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());
        Directory.CreateDirectory(_uploadsRoot);

        // Restored to the shared ApiFactory fixture's own values on Dispose (not null) -- ApiFactory
        // sets these exact variables once, lazily, the first time its host is built; nulling them out
        // here instead would permanently break any later test in the same process (e.g.
        // NpgsqlExportFilterTests) that reads them directly. The EventBus__* variables below have no
        // ApiFactory-set value to restore, so clearing them back to null (their original state) is correct.
        _envScope = new EnvironmentVariableScope(
            "ConnectionStrings__DefaultConnection", "EventBus__Provider", "ImageStorage__PublicBaseUrl", "ImageStorage__LocalRootPath");

        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _postgres.GetConnectionString());
        Environment.SetEnvironmentVariable("EventBus__Provider", "RabbitMq");
        Environment.SetEnvironmentVariable("EventBus__Host", _rabbitMq.Hostname);
        Environment.SetEnvironmentVariable("EventBus__Port", _rabbitMq.GetMappedPublicPort(5672).ToString());
        Environment.SetEnvironmentVariable("EventBus__VirtualHost", "/");
        Environment.SetEnvironmentVariable("EventBus__User", "guest");
        Environment.SetEnvironmentVariable("EventBus__Pass", "guest");
        Environment.SetEnvironmentVariable("EventBus__Exchange", "worker.readiness.test.events");
        Environment.SetEnvironmentVariable("EventBus__QueueName", "worker.readiness.test.queue");
        // Mirrors the API in the real compose topology: hands every consumer/outbox/background job to
        // a Worker -- except no Worker host is started in this test, so the queue has zero consumers.
        Environment.SetEnvironmentVariable("BackgroundProcessing__Enabled", "false");
        Environment.SetEnvironmentVariable("ImageStorage__PublicBaseUrl", "http://test.local");
        Environment.SetEnvironmentVariable("ImageStorage__LocalRootPath", _uploadsRoot);

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => builder.UseEnvironment("Development"));
    }

    [Fact]
    public async Task Ready_Should_Return200_While_AdminHealth_Reports_WorkerUnhealthy_When_NoConsumerExists()
    {
        using var readyClient = _factory!.CreateClient();

        // WorkerHealthCheck distinguishes "no consumer" from "broker unreachable" by passively
        // checking the main queue exists (QueueDeclarePassive) -- but that queue is only ever
        // declared lazily, by the first publish or the first consumer (RabbitMqTopology.Declare).
        // With BackgroundProcessing disabled on this host and no Worker started anywhere, nothing
        // would otherwise declare it, so publish one event here to stand in for the outbox publisher
        // that would normally do this in a real deployment.
        var eventBus = _factory.Services.GetRequiredService<IEventBus>();
        await eventBus.PublishAsync(new OrderCancelledIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), "readiness test"));

        HealthReportDto? readyReport = null;
        await WaitUntilAsync(async () =>
        {
            var readyResponse = await readyClient.GetAsync("/health/ready");
            if (readyResponse.StatusCode != HttpStatusCode.OK) return false;
            readyReport = await readyResponse.Content.ReadFromJsonAsync<HealthReportDto>(ApiEnvelopeJson.Options);
            return readyReport is not null;
        }, TimeSpan.FromSeconds(30));

        readyReport.Should().NotBeNull();
        readyReport!.Status.Should().NotBe("Unhealthy");
        readyReport.Checks.Should().ContainSingle(c => c.Name == "rabbitmq" && c.Status == "Healthy");
        readyReport.Checks.Should().NotContain(c => c.Name == "worker");

        using var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        HealthCheckDto? workerCheck = null;
        await WaitUntilAsync(async () =>
        {
            var adminResponse = await admin.GetAsync("/api/v1/admin/system/health");
            if (adminResponse.StatusCode != HttpStatusCode.OK) return false;
            var envelope = await adminResponse.Content.ReadFromJsonAsync<ApiEnvelope<HealthReportDto>>(ApiEnvelopeJson.Options);
            workerCheck = envelope?.Data?.Checks.FirstOrDefault(c => c.Name == "worker");
            return workerCheck is { Status: "Unhealthy" };
        }, TimeSpan.FromSeconds(30));

        workerCheck.Should().NotBeNull();
        workerCheck!.Status.Should().Be("Unhealthy");
        workerCheck.Description.Should().Be("no consumer: start the worker");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (await condition()) return;
            await Task.Delay(250);
        }

        throw new TimeoutException("Condition was not met within the timeout.");
    }

    public async Task DisposeAsync()
    {
        _factory?.Dispose();
        _envScope?.Dispose();
        Environment.SetEnvironmentVariable("EventBus__Host", null);
        Environment.SetEnvironmentVariable("EventBus__Port", null);
        Environment.SetEnvironmentVariable("EventBus__VirtualHost", null);
        Environment.SetEnvironmentVariable("EventBus__User", null);
        Environment.SetEnvironmentVariable("EventBus__Pass", null);
        Environment.SetEnvironmentVariable("EventBus__Exchange", null);
        Environment.SetEnvironmentVariable("EventBus__QueueName", null);
        Environment.SetEnvironmentVariable("BackgroundProcessing__Enabled", null);
        if (Directory.Exists(_uploadsRoot))
            Directory.Delete(_uploadsRoot, recursive: true);
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
    }
}
