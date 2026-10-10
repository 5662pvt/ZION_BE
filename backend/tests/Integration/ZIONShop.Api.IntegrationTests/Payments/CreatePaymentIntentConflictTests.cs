using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Payments.PaymentsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Payments;

[Collection(ApiTestCollection.Name)]
public class CreatePaymentIntentConflictTests
{
    private readonly ApiFactory _factory;

    public CreatePaymentIntentConflictTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task CreateIntent_Should_Return409_When_NoPayableOrderProjectionExists()
    {
        var (customer, _, _) = await CreateCustomerWithAddressAsync(_factory);

        var response = await CreatePaymentIntentRawAsync(customer, Guid.NewGuid(), Guid.NewGuid().ToString("N"));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<object>>(ApiEnvelopeJson.Options);
        envelope!.Message.Should().Be("Đơn hàng chưa sẵn sàng thanh toán");
    }
}
