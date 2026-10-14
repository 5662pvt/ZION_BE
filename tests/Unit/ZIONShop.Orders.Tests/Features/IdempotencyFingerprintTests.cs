using ZIONShop.Orders.Application.Features.PlaceOrder;

namespace ZIONShop.Orders.Tests.Features;

public class IdempotencyFingerprintTests
{
    [Fact]
    public void Compute_Should_ReturnSameHash_When_InputsAreSame()
    {
        var addressId = Guid.NewGuid();
        var a = IdempotencyFingerprint.Compute(addressId, "COD", "note", "SALE10");
        var b = IdempotencyFingerprint.Compute(addressId, "COD", "note", "SALE10");

        a.Should().Be(b);
    }

    [Fact]
    public void Compute_Should_IgnoreCase_When_PaymentMethodOrCouponCaseDiffers()
    {
        var addressId = Guid.NewGuid();
        var a = IdempotencyFingerprint.Compute(addressId, "COD", "note", "sale10");
        var b = IdempotencyFingerprint.Compute(addressId, "cod", "note", "SALE10");

        a.Should().Be(b);
    }

    [Fact]
    public void Compute_Should_IgnoreCase_When_AddressIdCaseDiffers()
    {
        var addressId = Guid.NewGuid();
        var a = IdempotencyFingerprint.Compute(addressId, "COD", null, null);
        var b = IdempotencyFingerprint.Compute(Guid.Parse(addressId.ToString().ToUpperInvariant()), "COD", null, null);

        a.Should().Be(b);
    }

    [Fact]
    public void Compute_Should_TrimNote_When_NoteHasSurroundingWhitespace()
    {
        var addressId = Guid.NewGuid();
        var a = IdempotencyFingerprint.Compute(addressId, "COD", "  giao giờ hành chính  ", null);
        var b = IdempotencyFingerprint.Compute(addressId, "COD", "giao giờ hành chính", null);

        a.Should().Be(b);
    }

    [Fact]
    public void Compute_Should_TreatNullAndEmpty_AsSame_When_NoteOrCouponMissing()
    {
        var addressId = Guid.NewGuid();
        var a = IdempotencyFingerprint.Compute(addressId, "COD", null, null);
        var b = IdempotencyFingerprint.Compute(addressId, "COD", "", "");

        a.Should().Be(b);
    }

    [Fact]
    public void Compute_Should_ReturnDifferentHash_When_AddressIdDiffers()
    {
        var a = IdempotencyFingerprint.Compute(Guid.NewGuid(), "COD", null, null);
        var b = IdempotencyFingerprint.Compute(Guid.NewGuid(), "COD", null, null);

        a.Should().NotBe(b);
    }

    [Fact]
    public void Compute_Should_ReturnDifferentHash_When_CouponCodeDiffers()
    {
        var addressId = Guid.NewGuid();
        var a = IdempotencyFingerprint.Compute(addressId, "COD", null, "SALE10");
        var b = IdempotencyFingerprint.Compute(addressId, "COD", null, "SALE20");

        a.Should().NotBe(b);
    }
}
