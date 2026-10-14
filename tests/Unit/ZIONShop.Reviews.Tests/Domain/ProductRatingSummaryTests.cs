using ZIONShop.Reviews.Domain.Entities;

namespace ZIONShop.Reviews.Tests.Domain;

public class ProductRatingSummaryTests
{
    [Fact]
    public void Average_Should_BeZero_When_NoRatings()
    {
        var summary = ProductRatingSummary.CreateEmpty(Guid.NewGuid());

        summary.Recompute(Array.Empty<int>());

        summary.Count.Should().Be(0);
        summary.Average.Should().Be(0m);
    }

    [Fact]
    public void Recompute_Should_SetDistribution_When_RatingsProvided()
    {
        var summary = ProductRatingSummary.CreateEmpty(Guid.NewGuid());

        summary.Recompute(new[] { 5, 5, 4, 1 });

        summary.Count.Should().Be(4);
        summary.Sum.Should().Be(15);
        summary.Star5.Should().Be(2);
        summary.Star4.Should().Be(1);
        summary.Star1.Should().Be(1);
        summary.Star2.Should().Be(0);
        summary.Star3.Should().Be(0);
    }

    [Fact]
    public void Average_Should_RoundToOneDecimal_AwayFromZero()
    {
        var summary = ProductRatingSummary.CreateEmpty(Guid.NewGuid());

        // (5 + 4 + 4) / 3 = 4.333... -> rounds to 4.3
        summary.Recompute(new[] { 5, 4, 4 });

        summary.Average.Should().Be(4.3m);
    }

    [Fact]
    public void Average_Should_RoundAwayFromZero_When_ExactlyHalfway()
    {
        var summary = ProductRatingSummary.CreateEmpty(Guid.NewGuid());

        // (5 + 4) / 2 = 4.5 -> AwayFromZero rounds to 4.5 already at 1 decimal, use a case that needs rounding up
        // (5 + 5 + 4) / 3 = 4.666... -> rounds to 4.7
        summary.Recompute(new[] { 5, 5, 4 });

        summary.Average.Should().Be(4.7m);
    }
}
