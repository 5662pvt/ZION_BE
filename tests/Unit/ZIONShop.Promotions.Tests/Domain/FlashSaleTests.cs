using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Enums;

namespace ZIONShop.Promotions.Tests.Domain;

public class FlashSaleTests
{
    [Fact]
    public void Activate_Should_SetIsActiveTrue_When_Inactive()
    {
        var flashSale = FlashSale.Create("Sale", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        var result = flashSale.Activate();

        result.IsSuccess.Should().BeTrue();
        flashSale.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Activate_Should_Fail_When_AlreadyActive()
    {
        var flashSale = FlashSale.Create("Sale", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));
        flashSale.Activate();

        var result = flashSale.Activate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.AlreadyActive");
    }

    [Fact]
    public void Deactivate_Should_Fail_When_AlreadyInactive()
    {
        var flashSale = FlashSale.Create("Sale", DateTime.UtcNow, DateTime.UtcNow.AddHours(1));

        var result = flashSale.Deactivate();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Promotions.AlreadyInactive");
    }

    [Fact]
    public void IsRunning_Should_ReturnFalse_When_ActiveButOutsideWindow()
    {
        var flashSale = FlashSale.Create("Sale", DateTime.UtcNow.AddDays(-2), DateTime.UtcNow.AddDays(-1));
        flashSale.Activate();

        flashSale.IsRunning(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsRunning_Should_ReturnTrue_When_ActiveAndWithinWindow()
    {
        var flashSale = FlashSale.Create("Sale", DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1));
        flashSale.Activate();

        flashSale.IsRunning(DateTime.UtcNow).Should().BeTrue();
    }
}

public class FlashSaleItemTests
{
    [Fact]
    public void TryReserve_Should_IncrementSoldQuantity_When_WithinAllocation()
    {
        var item = FlashSaleItem.Create(Guid.NewGuid(), Guid.NewGuid(), 50000m, allocatedQuantity: 10);

        var reserved = item.TryReserve(4);

        reserved.Should().BeTrue();
        item.SoldQuantity.Should().Be(4);
        item.Available.Should().Be(6);
    }

    [Fact]
    public void TryReserve_Should_Fail_When_QuantityExceedsRemainingAllocation()
    {
        var item = FlashSaleItem.Create(Guid.NewGuid(), Guid.NewGuid(), 50000m, allocatedQuantity: 10);
        item.TryReserve(8);

        var reserved = item.TryReserve(3); // only 2 left -- all-or-nothing, must fail entirely

        reserved.Should().BeFalse();
        item.SoldQuantity.Should().Be(8); // unchanged
    }

    [Fact]
    public void Release_Should_DecrementSoldQuantity_ButFloorAtZero()
    {
        var item = FlashSaleItem.Create(Guid.NewGuid(), Guid.NewGuid(), 50000m, allocatedQuantity: 10);
        item.TryReserve(3);

        item.Release(5); // releasing more than reserved must still floor at 0, never go negative

        item.SoldQuantity.Should().Be(0);
    }
}

public class FlashSaleRedemptionTests
{
    [Fact]
    public void Create_Should_StartAsReserved()
    {
        var redemption = FlashSaleRedemption.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, 50000m, DateTime.UtcNow);

        redemption.Status.Should().Be(RedemptionStatus.Reserved);
        redemption.UpdatedAt.Should().BeNull();
    }

    [Fact]
    public void MarkCommitted_Should_TransitionFromReserved()
    {
        var redemption = FlashSaleRedemption.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, 50000m, DateTime.UtcNow);
        var now = DateTime.UtcNow;

        redemption.MarkCommitted(now);

        redemption.Status.Should().Be(RedemptionStatus.Committed);
        redemption.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void MarkReleased_Should_TransitionFromReserved()
    {
        var redemption = FlashSaleRedemption.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2, 50000m, DateTime.UtcNow);
        var now = DateTime.UtcNow;

        redemption.MarkReleased(now);

        redemption.Status.Should().Be(RedemptionStatus.Released);
        redemption.UpdatedAt.Should().Be(now);
    }
}
