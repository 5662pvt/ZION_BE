using ZIONShop.Loyalty.Domain.Entities;

namespace ZIONShop.Loyalty.Tests.Domain;

public class PointAccountTests
{
    [Fact]
    public void Create_Should_StartAtZeroBalance()
    {
        var account = PointAccount.Create(Guid.NewGuid());

        account.Balance.Should().Be(0);
    }

    [Fact]
    public void Decrease_Should_NeverGoBelowZero()
    {
        var account = PointAccount.Create(Guid.NewGuid());
        account.Increase(50);

        account.Decrease(100);

        account.Balance.Should().Be(0);
    }

    [Fact]
    public void Increase_Should_Accumulate()
    {
        var account = PointAccount.Create(Guid.NewGuid());

        account.Increase(50);
        account.Increase(25);

        account.Balance.Should().Be(75);
    }
}
