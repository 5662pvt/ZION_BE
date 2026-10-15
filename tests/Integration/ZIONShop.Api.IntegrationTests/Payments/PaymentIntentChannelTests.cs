using System.Net;
using System.Net.Http.Json;
using ZIONShop.Api.IntegrationTests.Checkout;
using ZIONShop.Api.IntegrationTests.Infrastructure;
using static ZIONShop.Api.IntegrationTests.Checkout.CheckoutTestHelpers;
using static ZIONShop.Api.IntegrationTests.Payments.PaymentsTestHelpers;

namespace ZIONShop.Api.IntegrationTests.Payments;

[Collection(ApiTestCollection.Name)]
public class PaymentIntentChannelTests
{
    private readonly ApiFactory _factory;

    public PaymentIntentChannelTests(ApiFactory factory) => _factory = factory;

    private async Task<(HttpClient Customer, OrderData Order)> PlaceOnlineOrderAsync()
    {
        var admin = _factory.CreateClient();
        var login = await admin.LoginAsAdminAsync();
        admin.WithBearerToken(login.AccessToken);

        var (productId, _) = await CreatePublishedProductAsync(admin);
        await AdjustStockAsync(admin, productId, 10);

        var (customer, addressId, _) = await CreateCustomerWithAddressAsync(_factory);
        await AddToCartAsync(customer, productId, 1);

        var placed = await PlaceOrderAsync(customer, addressId, "Online");
        await WaitForOrderStatusAsync(customer, placed.Id, "AwaitingPayment");

        return (customer, placed);
    }

    [Fact]
    public async Task CreateIntent_Should_StoreEWalletChannel_When_Provided()
    {
        var (customer, order) = await PlaceOnlineOrderAsync();

        var intent = await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"), "EWallet");

        intent.Channel.Should().Be("EWallet");
    }

    [Fact]
    public async Task CreateIntent_Should_DefaultToCard_When_ChannelNotProvided()
    {
        var (customer, order) = await PlaceOnlineOrderAsync();

        var intent = await CreatePaymentIntentAsync(customer, order.Id, Guid.NewGuid().ToString("N"));

        intent.Channel.Should().Be("Card");
    }

    [Fact]
    public async Task CreateIntent_Should_Return400_When_ChannelIsInvalid()
    {
        var (customer, order) = await PlaceOnlineOrderAsync();

        var response = await CreatePaymentIntentRawAsync(customer, order.Id, Guid.NewGuid().ToString("N"), "PayPal");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiEnvelope<PaymentIntentData>>(ApiEnvelopeJson.Options);
        envelope!.Errors.Should().ContainSingle(e => e.Code == "Payments.InvalidChannel");
    }
}
