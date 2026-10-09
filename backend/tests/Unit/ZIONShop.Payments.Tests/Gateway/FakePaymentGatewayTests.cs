using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Tests.TestSupport;

namespace ZIONShop.Payments.Tests.Gateway;

public class FakePaymentGatewayTests
{
    [Fact]
    public async Task CreateIntentAsync_Should_GenerateFakePrefixed24HexIntentId()
    {
        var gateway = PaymentsTestFactory.BuildGateway();
        var payment = Payment.Create(Guid.NewGuid(), Guid.NewGuid(), 50000m, "VND", "Fake", "key", DateTime.UtcNow);

        var (intentId, redirectUrl) = await gateway.CreateIntentAsync(payment);

        intentId.Should().StartWith("fake_");
        intentId["fake_".Length..].Should().HaveLength(24);
        intentId["fake_".Length..].Should().MatchRegex("^[0-9a-f]{24}$");
        redirectUrl.Should().Be($"{PaymentsTestFactory.CheckoutBaseUrl}/checkout/pay/{intentId}");
    }

    [Fact]
    public void BuildRedirectUrl_Should_ComposeCheckoutBaseUrl_And_IntentId()
    {
        var gateway = PaymentsTestFactory.BuildGateway(checkoutBaseUrl: "http://localhost:3000/");
        gateway.BuildRedirectUrl("fake_abc").Should().Be("http://localhost:3000/checkout/pay/fake_abc");
    }

    [Fact]
    public void VerifySignature_Should_ReturnTrue_When_SignatureMatches()
    {
        var gateway = PaymentsTestFactory.BuildGateway();
        const string rawBody = "{\"eventId\":\"11111111-1111-1111-1111-111111111111\"}";

        var signature = gateway.SignPayload(rawBody);

        gateway.VerifySignature(rawBody, signature).Should().BeTrue();
    }

    [Fact]
    public void VerifySignature_Should_ReturnFalse_When_SignatureIsWrong()
    {
        var gateway = PaymentsTestFactory.BuildGateway();
        const string rawBody = "{\"eventId\":\"11111111-1111-1111-1111-111111111111\"}";

        gateway.VerifySignature(rawBody, "0000000000000000000000000000000000000000000000000000000000000000").Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_Should_ReturnFalse_When_BodyWasTampered()
    {
        var gateway = PaymentsTestFactory.BuildGateway();
        var signature = gateway.SignPayload("{\"result\":\"succeeded\"}");

        gateway.VerifySignature("{\"result\":\"failed\"}", signature).Should().BeFalse();
    }

    [Fact]
    public void VerifySignature_Should_ReturnFalse_When_SignatureIsEmpty()
    {
        var gateway = PaymentsTestFactory.BuildGateway();
        gateway.VerifySignature("{}", "").Should().BeFalse();
    }

    [Fact]
    public void SignPayload_Should_UseConfiguredSecret_So_DifferentSecretsProduceDifferentSignatures()
    {
        var gatewayA = PaymentsTestFactory.BuildGateway(webhookSecret: "secret-a");
        var gatewayB = PaymentsTestFactory.BuildGateway(webhookSecret: "secret-b");
        const string rawBody = "{\"x\":1}";

        gatewayA.SignPayload(rawBody).Should().NotBe(gatewayB.SignPayload(rawBody));
    }
}
