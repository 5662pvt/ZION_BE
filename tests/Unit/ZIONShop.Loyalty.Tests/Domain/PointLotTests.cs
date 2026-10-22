using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Enums;

namespace ZIONShop.Loyalty.Tests.Domain;

public class PointLotTests
{
    private static PointLot CreateLot(int points, DateTime? expiresAt = null) =>
        PointLot.Create(Guid.NewGuid(), PointLotSource.Order, Guid.NewGuid().ToString(), points, DateTime.UtcNow, expiresAt ?? DateTime.UtcNow.AddMonths(12));

    [Fact]
    public void Consume_Should_TakeNoMoreThanRemaining()
    {
        var lot = CreateLot(100);

        var taken = lot.Consume(150);

        taken.Should().Be(100);
        lot.Remaining.Should().Be(0);
    }

    [Fact]
    public void Consume_Should_TakeExactAmount_When_EnoughRemaining()
    {
        var lot = CreateLot(100);

        var taken = lot.Consume(30);

        taken.Should().Be(30);
        lot.Remaining.Should().Be(70);
    }

    [Fact]
    public void AddBack_Should_NeverExceedOriginal()
    {
        var lot = CreateLot(100);
        lot.Consume(40);

        lot.AddBack(1000);

        lot.Remaining.Should().Be(100);
    }

    [Fact]
    public void AddBack_Should_RestoreOnlyWhatWasGivenBack()
    {
        var lot = CreateLot(100);
        lot.Consume(40);

        lot.AddBack(10);

        lot.Remaining.Should().Be(70);
    }

    [Fact]
    public void ZeroOut_Should_RemoveWhateverRemains_AndNeverGoNegative()
    {
        var lot = CreateLot(100);
        lot.Consume(60);

        var removed = lot.ZeroOut();

        removed.Should().Be(40);
        lot.Remaining.Should().Be(0);

        var removedAgain = lot.ZeroOut();
        removedAgain.Should().Be(0);
        lot.Remaining.Should().Be(0);
    }
}
