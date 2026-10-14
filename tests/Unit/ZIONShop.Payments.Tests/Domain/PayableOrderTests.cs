using ZIONShop.Payments.Domain.Entities;
using ZIONShop.Payments.Domain.Enums;

namespace ZIONShop.Payments.Tests.Domain;

public class PayableOrderTests
{
    private static PayableOrder CreateAwaitingStock(DateTime? now = null) =>
        PayableOrder.Create(Guid.NewGuid(), Guid.NewGuid(), 100000m, "VND", now ?? DateTime.UtcNow);

    [Fact]
    public void Create_Should_StartInAwaitingStock()
    {
        var order = CreateAwaitingStock();
        order.State.Should().Be(PayableOrderState.AwaitingStock);
    }

    [Fact]
    public void MarkPayable_Should_Succeed_When_AwaitingStock()
    {
        var order = CreateAwaitingStock();
        var result = order.MarkPayable(DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        order.State.Should().Be(PayableOrderState.Payable);
    }

    [Fact]
    public void MarkPayable_Should_Fail_When_CalledTwice()
    {
        var order = CreateAwaitingStock();
        order.MarkPayable(DateTime.UtcNow);

        var result = order.MarkPayable(DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        order.State.Should().Be(PayableOrderState.Payable);
    }

    [Fact]
    public void MarkPaid_Should_Succeed_When_Payable()
    {
        var order = CreateAwaitingStock();
        order.MarkPayable(DateTime.UtcNow);

        var result = order.MarkPaid(DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        order.State.Should().Be(PayableOrderState.Paid);
    }

    [Fact]
    public void MarkPaid_Should_Fail_When_StillAwaitingStock()
    {
        var order = CreateAwaitingStock();

        var result = order.MarkPaid(DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        order.State.Should().Be(PayableOrderState.AwaitingStock);
    }

    [Fact]
    public void MarkCancelled_Should_Succeed_FromAwaitingStockOrPayable()
    {
        var order = CreateAwaitingStock();
        var result = order.MarkCancelled(DateTime.UtcNow);

        result.IsSuccess.Should().BeTrue();
        order.State.Should().Be(PayableOrderState.Cancelled);
    }

    [Fact]
    public void MarkCancelled_Should_Fail_When_AlreadyPaid()
    {
        var order = CreateAwaitingStock();
        order.MarkPayable(DateTime.UtcNow);
        order.MarkPaid(DateTime.UtcNow);

        var result = order.MarkCancelled(DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
        order.State.Should().Be(PayableOrderState.Paid);
    }

    [Fact]
    public void MarkCancelled_Should_Fail_When_AlreadyCancelled()
    {
        var order = CreateAwaitingStock();
        order.MarkCancelled(DateTime.UtcNow);

        var result = order.MarkCancelled(DateTime.UtcNow);

        result.IsFailure.Should().BeTrue();
    }
}
