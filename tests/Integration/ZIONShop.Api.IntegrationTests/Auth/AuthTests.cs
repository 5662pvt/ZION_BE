using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Auth;

[Collection(ApiTestCollection.Name)]
public class AuthTests
{
    private readonly ApiFactory _factory;

    public AuthTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Login_Should_ReturnTokens_When_CredentialsAreValid()
    {
        var client = _factory.CreateClient();

        var response = await client.LoginAsync(TestAuthHelpers.AdminEmail, TestAuthHelpers.AdminPassword);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options);
        envelope!.Success.Should().BeTrue();
        envelope.Data!.AccessToken.Should().NotBeNullOrWhiteSpace();
        envelope.Data.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_Should_ReturnFailureEnvelope_When_PasswordIsWrong()
    {
        var client = _factory.CreateClient();

        var response = await client.LoginAsync(TestAuthHelpers.AdminEmail, "not-the-right-password");

        ((int)response.StatusCode).Should().BeGreaterThanOrEqualTo(400).And.BeLessThan(500);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Refresh_Should_RotateToken_And_RejectThePreviousRefreshToken()
    {
        var client = _factory.CreateClient();
        var login = await client.LoginAsAdminAsync();

        var refreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
        refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshed = await refreshResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options);
        refreshed!.Data!.RefreshToken.Should().NotBe(login.RefreshToken);

        var reuseResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = login.RefreshToken });
        ((int)reuseResponse.StatusCode).Should().BeGreaterThanOrEqualTo(400).And.BeLessThan(500);
        var reuseEnvelope = await reuseResponse.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        reuseEnvelope!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task Me_Should_ReturnAdminRole_When_CalledWithAdminBearerToken()
    {
        var client = _factory.CreateClient();
        var login = await client.LoginAsAdminAsync();
        client.WithBearerToken(login.AccessToken);

        var response = await client.GetAsync("/api/v1/auth/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<UserData>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Roles.Should().Contain("Admin");
    }

    private record UserData(Guid Id, string Email, string? DisplayName, List<string> Roles);
}
