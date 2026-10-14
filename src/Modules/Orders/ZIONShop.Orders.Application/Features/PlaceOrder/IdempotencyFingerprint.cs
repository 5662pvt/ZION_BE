using System.Security.Cryptography;
using System.Text;

namespace ZIONShop.Orders.Application.Features.PlaceOrder;

/// <summary>
/// SHA-256 fingerprint of the normalized place-order body, used to detect whether a repeated
/// Idempotency-Key is replaying the exact same request or reusing the key for a different one.
/// </summary>
public static class IdempotencyFingerprint
{
    public static string Compute(Guid addressId, string paymentMethod, string? note, string? couponCode)
    {
        var normalized = string.Join('|',
            addressId.ToString().ToLowerInvariant(),
            (paymentMethod ?? string.Empty).ToLowerInvariant(),
            (note ?? string.Empty).Trim(),
            (couponCode ?? string.Empty).Trim().ToUpperInvariant());

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash);
    }
}
