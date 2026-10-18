using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Users;

[Collection(ApiTestCollection.Name)]
public class GeoTests
{
    private readonly ApiFactory _factory;

    public GeoTests(ApiFactory factory) => _factory = factory;

    private record ProvinceData(Guid Id, string Code, string Name);
    private record WardData(Guid Id, string Code, string Name);

    [Fact]
    public async Task GetProvinces_Should_Return34Provinces()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/geo/provinces");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<ProvinceData>>>(ApiEnvelopeJson.Options);
        body!.Data!.Should().HaveCount(34);
    }

    [Fact]
    public async Task GetWards_Should_ReturnNonEmptyList_When_ProvinceExists()
    {
        var client = _factory.CreateClient();

        var provincesResponse = await client.GetAsync("/api/v1/geo/provinces");
        var provinces = await provincesResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<ProvinceData>>>(ApiEnvelopeJson.Options);
        var hanoi = provinces!.Data!.Single(p => p.Code == "01");

        var wardsResponse = await client.GetAsync($"/api/v1/geo/provinces/{hanoi.Id}/wards");
        wardsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var wards = await wardsResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<WardData>>>(ApiEnvelopeJson.Options);
        wards!.Data!.Should().NotBeEmpty();
        wards.Data.Should().Contain(w => w.Name == "Phường Ba Đình");
    }

    [Fact]
    public async Task GetWards_Should_ReturnNotFound_When_ProvinceIdIsBogus()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/api/v1/geo/provinces/{Guid.NewGuid()}/wards");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
