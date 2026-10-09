using System.Net;
using System.Net.Http.Json;
using System.Text;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Errors;

[Collection(ApiTestCollection.Name)]
public class ErrorsTests
{
    private readonly ApiFactory _factory;

    public ErrorsTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MalformedJsonBody_Should_Return400_With_ValidationFailedEnvelope()
    {
        var client = _factory.CreateClient();
        var content = new StringContent("{ \"email\": \"a@b.com\", ", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/api/v1/auth/login", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Success.Should().BeFalse();
        envelope.Message.Should().Be("Validation failed");
    }
}
