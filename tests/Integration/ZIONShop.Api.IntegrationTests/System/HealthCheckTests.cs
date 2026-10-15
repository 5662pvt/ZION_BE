using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.SystemOps;

[Collection(ApiTestCollection.Name)]
public class HealthCheckTests
{
    private readonly ApiFactory _factory;

    public HealthCheckTests(ApiFactory factory) => _factory = factory;

    private record HealthCheckDto(string Name, string Status, double DurationMs, string? Description);
    private record HealthReportDto(string Status, double TotalDurationMs, List<HealthCheckDto> Checks);

    [Fact]
    public async Task Live_Should_Return200()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/live");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Ready_Should_Return200_WithExpectedChecks_And_NoSecretsInBody()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotContain("Password");
        body.Should().NotContain("Host=");

        var report = await response.Content.ReadFromJsonAsync<HealthReportDto>(ApiEnvelopeJson.Options);
        report!.Checks.Should().ContainSingle(c => c.Name == "postgres" && c.Status == "Healthy");
        report.Checks.Should().ContainSingle(c => c.Name == "rabbitmq" && c.Description == "in-memory provider");
        // Redis is optional and fails open -- it must never make readiness fail, whether or not a
        // local Redis happens to be reachable in the environment running this test.
        report.Checks.Should().ContainSingle(c => c.Name == "redis" && c.Status != "Unhealthy");
        // "worker" is registered without the "ready" tag (Program.cs) -- whether anyone runs the
        // Worker is an operational fact, not a reason for a load balancer to fail this probe. It must
        // never appear in /health/ready at all, regardless of its own status.
        report.Checks.Should().NotContain(c => c.Name == "worker");
    }
}
