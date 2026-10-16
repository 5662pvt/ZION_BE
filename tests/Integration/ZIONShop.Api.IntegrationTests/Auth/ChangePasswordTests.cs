using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Auth;

[Collection(ApiTestCollection.Name)]
public class ChangePasswordTests
{
    private readonly ApiFactory _factory;

    public ChangePasswordTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task ChangePassword_Should_RevokeOldRefreshToken_And_AllowLoginWithNewPassword()
    {
        var client = _factory.CreateClient();
        var email = $"changepw-{Guid.NewGuid():N}@zionshop.local";
        const string oldPassword = "OldPassword@123";
        const string newPassword = "NewPassword@123";
        await client.RegisterAndConfirmAsync(_factory, email, oldPassword, "Change Password User");

        var login = await client.LoginAsync(email, oldPassword);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginData = (await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options))!.Data!;

        client.WithBearerToken(loginData.AccessToken);
        var changeResponse = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = oldPassword, newPassword });
        changeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var changed = (await changeResponse.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options))!.Data!;
        changed.AccessToken.Should().NotBeNullOrWhiteSpace();
        changed.RefreshToken.Should().NotBe(loginData.RefreshToken);

        var oldRefreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = loginData.RefreshToken });
        ((int)oldRefreshResponse.StatusCode).Should().BeGreaterThanOrEqualTo(400).And.BeLessThan(500);

        var newRefreshResponse = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = changed.RefreshToken });
        newRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var loginWithNewPassword = await client.LoginAsync(email, newPassword);
        loginWithNewPassword.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_When_CurrentPasswordIsWrong()
    {
        var client = _factory.CreateClient();
        var email = $"changepw-wrong-{Guid.NewGuid():N}@zionshop.local";
        await client.RegisterAndConfirmAsync(_factory, email, "OldPassword@123", "User");
        var login = await client.LoginAsync(email, "OldPassword@123");
        var loginData = (await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options))!.Data!;
        client.WithBearerToken(loginData.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "WrongPassword@123", newPassword = "NewPassword@123" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors!.Should().Contain(e => e.Code == "Auth.InvalidCurrentPassword");
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnBadRequest_When_NewPasswordEqualsCurrent()
    {
        var client = _factory.CreateClient();
        var email = $"changepw-same-{Guid.NewGuid():N}@zionshop.local";
        await client.RegisterAndConfirmAsync(_factory, email, "SamePassword@123", "User");
        var login = await client.LoginAsync(email, "SamePassword@123");
        var loginData = (await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options))!.Data!;
        client.WithBearerToken(loginData.AccessToken);

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "SamePassword@123", newPassword = "SamePassword@123" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors!.Should().Contain(e => e.Code == "Auth.PasswordUnchanged");
    }

    [Fact]
    public async Task ChangePassword_Should_ReturnUnauthorized_When_NoBearerToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/change-password", new { currentPassword = "Whatever@123", newPassword = "NewPassword@123" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
