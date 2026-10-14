using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ZIONShop.Auth.Infrastructure.Persistence;

namespace ZIONShop.Api.IntegrationTests.Infrastructure;

public record AuthTokenData(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, Guid UserId, string Email, List<string> Roles);

public static class TestAuthHelpers
{
    public const string AdminEmail = "admin@zionshop.local";
    public const string AdminPassword = "Admin@123";

    public static async Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password)
        => await client.PostAsJsonAsync("/api/v1/auth/login", new { email, password });

    public static async Task<AuthTokenData> LoginAsAdminAsync(this HttpClient client)
    {
        var response = await client.LoginAsync(AdminEmail, AdminPassword);
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options);
        return envelope!.Data!;
    }

    public static HttpClient WithBearerToken(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    /// <summary>
    /// Registers a user through the public API, then bypasses the email-OTP verification step by
    /// confirming the email directly via AuthDbContext — the OTP code itself is only ever delivered
    /// through IEmailSender (logged, not persisted in plaintext), so there is no clean way to drive
    /// the real /verify-email flow from an HTTP-only test client.
    /// </summary>
    public static async Task RegisterAndConfirmAsync(this HttpClient client, ApiFactory factory, string email, string password, string displayName)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new { email, password, displayName });
        response.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email.Trim().ToLowerInvariant());
        if (!user.EmailConfirmed)
        {
            user.ConfirmEmail();
            await db.SaveChangesAsync();
        }
    }
}
