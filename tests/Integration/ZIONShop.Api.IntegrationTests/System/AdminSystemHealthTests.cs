using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;

namespace ZIONShop.Api.IntegrationTests.SystemOps;

[Collection(ApiTestCollection.Name)]
public class AdminSystemHealthTests
{
    private readonly ApiFactory _factory;

    public AdminSystemHealthTests(ApiFactory factory) => _factory = factory;

    private record HealthCheckDto(string Name, string Status, double DurationMs, string? Description);
    private record HealthReportDto(string Status, double TotalDurationMs, List<HealthCheckDto> Checks);

    [Fact]
    public async Task Health_Should_Return200_ForAdmin()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var response = await admin.GetAsync("/api/v1/admin/system/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Success.Should().BeTrue();
    }

    [Fact]
    public async Task Health_Should_IncludeWorkerCheck_UnlikeHealthReady()
    {
        // Unlike /health/ready (tag "ready"), this admin endpoint runs every registered health check
        // with no tag filter, so "worker" (deliberately untagged -- see Program.cs) must still show up
        // here even though it is excluded from the readiness probe.
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var response = await admin.GetAsync("/api/v1/admin/system/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<HealthReportDto>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Checks.Should().ContainSingle(c => c.Name == "worker" && c.Description == "in-memory provider");
    }

    [Fact]
    public async Task Health_Should_Return403_ForCustomer()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await customer.GetAsync("/api/v1/admin/system/health");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Health_Should_Return401_ForAnonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/system/health");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
