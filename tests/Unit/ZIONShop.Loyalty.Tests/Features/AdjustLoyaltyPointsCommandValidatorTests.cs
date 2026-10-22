using ZIONShop.Loyalty.Application.Features.AdjustLoyaltyPoints;

namespace ZIONShop.Loyalty.Tests.Features;

public class AdjustLoyaltyPointsCommandValidatorTests
{
    private readonly AdjustLoyaltyPointsCommandValidator _validator = new();

    [Fact]
    public void Should_Fail_When_DeltaIsZero()
    {
        var result = _validator.Validate(new AdjustLoyaltyPointsCommand(Guid.NewGuid(), 0, "reason", Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_ReasonIsEmpty()
    {
        var result = _validator.Validate(new AdjustLoyaltyPointsCommand(Guid.NewGuid(), 100, "", Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_ReasonExceedsMaxLength()
    {
        var result = _validator.Validate(new AdjustLoyaltyPointsCommand(Guid.NewGuid(), 100, new string('a', 501), Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Fail_When_DeltaExceedsMagnitudeLimit()
    {
        var result = _validator.Validate(new AdjustLoyaltyPointsCommand(Guid.NewGuid(), 1_000_001, "reason", Guid.NewGuid()));

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Should_Pass_When_Valid()
    {
        var result = _validator.Validate(new AdjustLoyaltyPointsCommand(Guid.NewGuid(), -100, "reason", Guid.NewGuid()));

        result.IsValid.Should().BeTrue();
    }
}
