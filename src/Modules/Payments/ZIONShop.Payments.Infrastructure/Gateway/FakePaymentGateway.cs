using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ZIONShop.Payments.Application.Interfaces;
using ZIONShop.Payments.Application.Options;
using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Infrastructure.Gateway;

public class FakePaymentGateway : IPaymentGateway
{
    private readonly FakeGatewayOptions _options;

    public FakePaymentGateway(IOptions<FakeGatewayOptions> options) => _options = options.Value;

    public Task<(string IntentId, string RedirectUrl)> CreateIntentAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var intentId = "fake_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        return Task.FromResult((intentId, BuildRedirectUrl(intentId)));
    }

    public string BuildRedirectUrl(string intentId) => $"{_options.CheckoutBaseUrl.TrimEnd('/')}/checkout/pay/{intentId}";

    public string SignPayload(string rawBody)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_options.WebhookSecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(rawBody));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool VerifySignature(string rawBody, string signature)
    {
        if (string.IsNullOrWhiteSpace(signature)) return false;

        var expected = Encoding.UTF8.GetBytes(SignPayload(rawBody));
        var actual = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
        if (expected.Length != actual.Length) return false;

        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }
}
