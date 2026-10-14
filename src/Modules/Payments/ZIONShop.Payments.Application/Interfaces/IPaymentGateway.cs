using ZIONShop.Payments.Domain.Entities;

namespace ZIONShop.Payments.Application.Interfaces;

public interface IPaymentGateway
{
    /// <summary>Creates a provider-side checkout intent for a freshly-constructed (not-yet-persisted) payment.</summary>
    Task<(string IntentId, string RedirectUrl)> CreateIntentAsync(Payment payment, CancellationToken cancellationToken = default);

    /// <summary>Recomputes the checkout redirect URL for an already-assigned intent, so it never needs to be persisted.</summary>
    string BuildRedirectUrl(string intentId);

    /// <summary>Signs a raw webhook body the same way the provider would (fake gateway only).</summary>
    string SignPayload(string rawBody);

    /// <summary>Verifies a webhook signature against the raw request body using a constant-time comparison.</summary>
    bool VerifySignature(string rawBody, string signature);
}
