using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;

namespace ZIONShop.Api.IntegrationTests.Users;

[Collection(ApiTestCollection.Name)]
public class UsersAddressTests
{
    private readonly ApiFactory _factory;

    public UsersAddressTests(ApiFactory factory) => _factory = factory;

    private record AddressData(Guid Id, string RecipientName, string PhoneNumber, string Province, string Ward, string StreetAddress, bool IsDefault);

    [Fact]
    public async Task AddAddress_Should_AppearInList_With_VietnameseAddressShape()
    {
        var client = _factory.CreateClient();
        var login = await client.LoginAsAdminAsync();
        client.WithBearerToken(login.AccessToken);

        var addResponse = await client.PostAsJsonAsync("/api/v1/users/me/addresses", new
        {
            recipientName = "Nguyen Van A",
            phoneNumber = "0901234567",
            province = "TP.HCM",
            ward = "Phuong Ben Nghe",
            streetAddress = "123 Main St",
            isDefault = true
        });
        addResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var added = await addResponse.Content.ReadFromJsonAsync<ApiEnvelope<AddressData>>(ApiEnvelopeJson.Options);
        added!.Data!.RecipientName.Should().Be("Nguyen Van A");
        added.Data.PhoneNumber.Should().Be("0901234567");
        added.Data.Province.Should().Be("TP.HCM");
        added.Data.Ward.Should().Be("Phuong Ben Nghe");
        added.Data.StreetAddress.Should().Be("123 Main St");

        var listResponse = await client.GetAsync("/api/v1/users/me/addresses");
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiEnvelope<List<AddressData>>>(ApiEnvelopeJson.Options);
        list!.Data!.Should().Contain(a =>
            a.Id == added.Data.Id &&
            a.Province == "TP.HCM" &&
            a.Ward == "Phuong Ben Nghe" &&
            a.StreetAddress == "123 Main St");
    }
}
