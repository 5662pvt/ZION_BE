using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Enums;
using ZIONShop.Orders.Domain.ValueObjects;

namespace ZIONShop.Orders.Tests.Domain;

public class OrderStateMachineTests
{
    private static Order CreateOrder(PaymentMethod paymentMethod = PaymentMethod.COD) => Order.Place(
        Guid.NewGuid(),
        paymentMethod,
        "VND",
        30000,
        new ShippingAddress(Guid.NewGuid(), "Nguyen Van A", "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC"),
        new[] { new OrderLineInput(Guid.NewGuid(), "Widget", 100000m, 2) },
        null);

    [Fact]
    public void ConfirmReservation_Should_SetConfirmed_When_PaymentMethodIsCod()
    {
        var order = CreateOrder(PaymentMethod.COD);
        var result = order.ConfirmReservation();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Confirmed);
    }

    [Fact]
    public void ConfirmReservation_Should_SetAwaitingPayment_When_PaymentMethodIsOnline()
    {
        var order = CreateOrder(PaymentMethod.Online);
        var result = order.ConfirmReservation();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.AwaitingPayment);
    }

    [Fact]
    public void ConfirmReservation_Should_Fail_When_NotPendingStock()
    {
        var order = CreateOrder();
        order.ConfirmReservation();

        var result = order.ConfirmReservation();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidTransition");
    }

    [Fact]
    public void ExpireReservation_Should_Cancel_When_AwaitingPayment()
    {
        var order = CreateOrder(PaymentMethod.Online);
        order.ConfirmReservation();

        var result = order.ExpireReservation();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelReason.Should().Be("Quá hạn thanh toán");
    }

    [Fact]
    public void ExpireReservation_Should_Fail_When_NotAwaitingPayment()
    {
        var order = CreateOrder(PaymentMethod.COD);
        order.ConfirmReservation(); // -> Confirmed

        var result = order.ExpireReservation();

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CompletePayment_Should_SetPaid_When_AwaitingPayment()
    {
        var order = CreateOrder(PaymentMethod.Online);
        order.ConfirmReservation();

        var result = order.CompletePayment();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Paid);
    }

    [Fact]
    public void Ship_Should_Succeed_When_ConfirmedOrPaid()
    {
        var confirmed = CreateOrder(PaymentMethod.COD);
        confirmed.ConfirmReservation();
        confirmed.Ship().IsSuccess.Should().BeTrue();
        confirmed.Status.Should().Be(OrderStatus.Shipped);

        var paid = CreateOrder(PaymentMethod.Online);
        paid.ConfirmReservation();
        paid.CompletePayment();
        paid.Ship().IsSuccess.Should().BeTrue();
        paid.Status.Should().Be(OrderStatus.Shipped);
    }

    [Fact]
    public void Ship_Should_Fail_When_PendingStock()
    {
        var order = CreateOrder();
        var result = order.Ship();
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Complete_Should_SetCompleted_When_Shipped()
    {
        var order = CreateOrder(PaymentMethod.COD);
        order.ConfirmReservation();
        order.Ship();

        var result = order.Complete();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Completed);
    }

    [Fact]
    public void Complete_Should_Fail_When_NotShipped()
    {
        var order = CreateOrder();
        var result = order.Complete();
        result.IsFailure.Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancel_Should_Succeed_Before_Shipped(bool isStaff)
    {
        var order = CreateOrder(PaymentMethod.COD);
        var result = order.Cancel("Đổi ý", isStaff);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
        order.CancelReason.Should().Be("Đổi ý");
    }

    [Fact]
    public void Cancel_Should_Fail_When_Owner_And_AlreadyPaid()
    {
        var order = CreateOrder(PaymentMethod.Online);
        order.ConfirmReservation();
        order.CompletePayment(); // -> Paid

        var result = order.Cancel("Đổi ý", isStaff: false);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.CancelNotAllowed");
    }

    [Fact]
    public void Cancel_Should_Succeed_When_Staff_And_Paid()
    {
        var order = CreateOrder(PaymentMethod.Online);
        order.ConfirmReservation();
        order.CompletePayment(); // -> Paid

        var result = order.Cancel("Staff hủy", isStaff: true);

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public void Cancel_Should_Fail_When_AlreadyShipped()
    {
        var order = CreateOrder(PaymentMethod.COD);
        order.ConfirmReservation();
        order.Ship();

        var result = order.Cancel("Quá muộn", isStaff: true);

        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MarkReturned_Should_Succeed_When_Completed()
    {
        var order = CreateOrder(PaymentMethod.COD);
        order.ConfirmReservation();
        order.Ship();
        order.Complete();

        var result = order.MarkReturned();

        result.IsSuccess.Should().BeTrue();
        order.Status.Should().Be(OrderStatus.Returned);
    }

    [Fact]
    public void MarkReturned_Should_Fail_When_NotCompleted()
    {
        var order = CreateOrder(PaymentMethod.COD);
        order.ConfirmReservation();
        order.Ship();

        var result = order.MarkReturned();

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Orders.InvalidTransition");
    }
}
