using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Admin;

[Collection(ApiTestCollection.Name)]
public class AdminCustomersTests
{
    private readonly ApiFactory _factory;

    public AdminCustomersTests(ApiFactory factory) => _factory = factory;

    private const string StaffEmail = "staff@zionshop.local";
    private const string StaffPassword = "Staff@123";

    private record CustomerListItem(Guid Id, string Email, string? DisplayName, string? PhoneNumber, List<string> Roles, bool IsActive, bool EmailConfirmed, DateTime CreatedAt, DateTime? LastLoginAt);
    private record CustomerDetail(Guid Id, string Email, string? DisplayName, string? PhoneNumber, List<string> Roles, bool IsActive, bool EmailConfirmed, DateTime CreatedAt, DateTime? LastLoginAt, int AddressesCount);
    private record LockStatus(Guid Id, bool IsActive);

    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);
        return admin;
    }

    private async Task<(HttpClient Client, Guid UserId, string Email)> CreateCustomerAsync(string? phoneNumber = null)
    {
        var client = _factory.CreateClient();
        var email = $"customer-{Guid.NewGuid():N}@zionshop.local";
        await client.RegisterAndConfirmAsync(_factory, email, "Password@123", "Nguyen Van Test");
        var login = await client.LoginAsync(email, "Password@123");
        var loginData = (await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options))!.Data!;
        client.WithBearerToken(loginData.AccessToken);

        if (phoneNumber is not null)
        {
            var updateResponse = await client.PutAsJsonAsync("/api/v1/users/me", new { fullName = "Nguyen Van Test", phoneNumber, dateOfBirth = (DateTime?)null });
            updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        return (client, loginData.UserId, email);
    }

    [Fact]
    public async Task List_Should_FindCustomer_When_KeywordMatchesEmail()
    {
        var admin = await AdminClientAsync();
        var (_, _, email) = await CreateCustomerAsync();

        var response = await admin.GetAsync($"/api/v1/admin/customers?keyword={Uri.EscapeDataString(email)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<CustomerListItem>>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Should().ContainSingle(c => c.Email == email);
    }

    [Fact]
    public async Task List_Should_FindCustomer_When_KeywordMatchesPhone()
    {
        var admin = await AdminClientAsync();
        var phone = $"09{Random.Shared.Next(10000000, 99999999)}";
        var (_, userId, _) = await CreateCustomerAsync(phone);

        var response = await admin.GetAsync($"/api/v1/admin/customers?keyword={Uri.EscapeDataString(phone)}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<List<CustomerListItem>>>(ApiEnvelopeJson.Options);
        envelope!.Data!.Should().Contain(c => c.Id == userId && c.PhoneNumber == phone);
    }

    [Fact]
    public async Task GetById_Should_ReturnAddressesCount_When_CustomerExists()
    {
        var admin = await AdminClientAsync();
        var (client, userId, _) = await CreateCustomerAsync();
        var addResponse = await client.PostAsJsonAsync("/api/v1/users/me/addresses", new
        {
            recipientName = "Nguyen Van Test",
            phoneNumber = "0901234567",
            province = "TP.HCM",
            ward = "Phuong Ben Nghe",
            streetAddress = "123 Main St",
            isDefault = true
        });
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await admin.GetAsync($"/api/v1/admin/customers/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<CustomerDetail>>(ApiEnvelopeJson.Options);
        envelope!.Data!.AddressesCount.Should().Be(1);
    }

    [Fact]
    public async Task Lock_Should_DeactivateCustomer_And_RejectFutureLoginAndRefresh()
    {
        var admin = await AdminClientAsync();
        var (client, userId, email) = await CreateCustomerAsync();
        var loginBeforeLock = await client.LoginAsync(email, "Password@123");
        var tokensBeforeLock = (await loginBeforeLock.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options))!.Data!;

        var lockResponse = await admin.PostAsync($"/api/v1/admin/customers/{userId}/lock", content: null);

        lockResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var lockStatus = await lockResponse.Content.ReadFromJsonAsync<ApiEnvelope<LockStatus>>(ApiEnvelopeJson.Options);
        lockStatus!.Data!.IsActive.Should().BeFalse();

        var loginAfterLock = await client.LoginAsync(email, "Password@123");
        ((int)loginAfterLock.StatusCode).Should().BeGreaterThanOrEqualTo(400).And.BeLessThan(500);
        var loginEnvelope = await loginAfterLock.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        loginEnvelope!.Errors!.Should().Contain(e => e.Code == "Auth.UserInactive");

        var refreshAfterLock = await client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = tokensBeforeLock.RefreshToken });
        ((int)refreshAfterLock.StatusCode).Should().BeGreaterThanOrEqualTo(400).And.BeLessThan(500);

        var unlockResponse = await admin.PostAsync($"/api/v1/admin/customers/{userId}/unlock", content: null);
        unlockResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var unlockStatus = await unlockResponse.Content.ReadFromJsonAsync<ApiEnvelope<LockStatus>>(ApiEnvelopeJson.Options);
        unlockStatus!.Data!.IsActive.Should().BeTrue();

        var loginAfterUnlock = await client.LoginAsync(email, "Password@123");
        loginAfterUnlock.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Lock_Should_ReturnBadRequest_When_AdminLocksSelf()
    {
        var admin = await AdminClientAsync();
        var me = await admin.GetAsync("/api/v1/auth/me");
        var meData = await me.Content.ReadFromJsonAsync<ApiEnvelope<MeData>>(ApiEnvelopeJson.Options);

        var response = await admin.PostAsync($"/api/v1/admin/customers/{meData!.Data!.Id}/lock", content: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Errors!.Should().Contain(e => e.Code == "Admin.CannotLockSelf");
    }

    [Fact]
    public async Task List_Should_ReturnForbidden_When_CallerIsCustomer()
    {
        var (client, _, _) = await CreateCustomerAsync();

        var response = await client.GetAsync("/api/v1/admin/customers");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task List_Should_ReturnUnauthorized_When_CallerIsAnonymous()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/admin/customers");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Staff_Should_AccessStaffOrAdminEndpoint_But_NotAdminOnlyEndpoint()
    {
        var client = _factory.CreateClient();
        var login = await client.LoginAsync(StaffEmail, StaffPassword);
        login.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginData = (await login.Content.ReadFromJsonAsync<ApiEnvelope<AuthTokenData>>(ApiEnvelopeJson.Options))!.Data!;
        loginData.Roles.Should().Contain("Staff");
        client.WithBearerToken(loginData.AccessToken);

        var staffOrAdminResponse = await client.GetAsync("/api/v1/orders");
        staffOrAdminResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var adminOnlyResponse = await client.GetAsync("/api/v1/admin/customers");
        adminOnlyResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private record MeData(Guid Id, string Email, string? DisplayName, List<string> Roles);
}
