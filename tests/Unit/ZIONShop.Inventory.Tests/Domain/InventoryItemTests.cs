using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Exceptions;

namespace ZIONShop.Inventory.Tests.Domain;

public class InventoryItemTests
{
    [Fact]
    public void Reserve_Should_IncreaseReserved_When_EnoughAvailable()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 10);

        var result = item.Reserve(4);

        result.IsSuccess.Should().BeTrue();
        item.Reserved.Should().Be(4);
        item.Available.Should().Be(6);
    }

    [Fact]
    public void Reserve_Should_Fail_When_QuantityExceedsAvailable()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 3);

        var result = item.Reserve(4);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.InsufficientStock);
        item.Reserved.Should().Be(0);
    }

    [Fact]
    public void Reserve_Should_Fail_When_QuantityIsZeroOrNegative()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 10);

        item.Reserve(0).IsFailure.Should().BeTrue();
        item.Reserve(-1).IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Release_Should_NeverGoNegative_When_ReleasingMoreThanReserved()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 10);
        item.Reserve(2);

        var result = item.Release(5);

        result.IsSuccess.Should().BeTrue();
        item.Reserved.Should().Be(0);
    }

    [Fact]
    public void Commit_Should_DecreaseOnHandAndReserved_When_Valid()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 10);
        item.Reserve(4);

        var result = item.Commit(4);

        result.IsSuccess.Should().BeTrue();
        item.OnHand.Should().Be(6);
        item.Reserved.Should().Be(0);
    }

    [Fact]
    public void Commit_Should_Fail_When_QuantityExceedsReserved()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 10);
        item.Reserve(2);

        var result = item.Commit(3);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.InsufficientReserved);
    }

    [Fact]
    public void Return_Should_IncreaseOnHand_When_Valid()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 10);
        item.Reserve(4);
        item.Commit(4);

        var result = item.Return(4);

        result.IsSuccess.Should().BeTrue();
        item.OnHand.Should().Be(10);
        item.Reserved.Should().Be(0);
    }

    [Fact]
    public void Return_Should_Fail_When_QuantityIsZeroOrNegative()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 10);

        item.Return(0).IsFailure.Should().BeTrue();
        item.Return(-1).IsFailure.Should().BeTrue();
        item.OnHand.Should().Be(10);
    }

    [Fact]
    public void Adjust_Should_IncreaseOnHand_When_PositiveDelta()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 10);

        var result = item.Adjust(5, "Restock");

        result.IsSuccess.Should().BeTrue();
        item.OnHand.Should().Be(15);
    }

    [Fact]
    public void Adjust_Should_Fail_When_ResultWouldBeNegative()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 5);

        var result = item.Adjust(-10, "Damage");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.InvalidAdjustment);
        item.OnHand.Should().Be(5);
    }

    [Fact]
    public void Adjust_Should_Fail_When_ResultWouldDropBelowReserved()
    {
        var item = InventoryItem.Create(Guid.NewGuid(), Guid.NewGuid(), "SKU-1", "Product 1", null, onHand: 10);
        item.Reserve(8);

        var result = item.Adjust(-5, "Damage");

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(InventoryErrors.AdjustmentBelowReserved);
        item.OnHand.Should().Be(10);
    }
}
