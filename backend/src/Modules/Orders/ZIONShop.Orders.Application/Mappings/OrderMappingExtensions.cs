using ZIONShop.Orders.Application.DTOs;
using ZIONShop.Orders.Domain.Entities;

namespace ZIONShop.Orders.Application.Mappings;

public static class OrderMappingExtensions
{
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.Code,
        order.Status.ToString(),
        order.PaymentMethod.ToString(),
        order.Subtotal,
        order.ShippingFee,
        order.CouponCode,
        order.DiscountAmount,
        order.DiscountTarget,
        order.Total,
        order.Currency,
        order.CreatedDate,
        order.Note,
        order.CancelReason,
        new ShippingAddressDto(
            order.ShippingAddress.AddressId,
            order.ShippingAddress.RecipientName,
            order.ShippingAddress.PhoneNumber,
            order.ShippingAddress.Province,
            order.ShippingAddress.Ward,
            order.ShippingAddress.StreetAddress),
        order.Lines.Select(l => new OrderLineDto(l.ProductId, l.ProductName, l.UnitPrice, l.Quantity, l.LineTotal)).ToList(),
        order.StatusHistory
            .OrderBy(h => h.At)
            .Select(h => new OrderStatusHistoryDto(h.Status.ToString(), h.At, h.Note))
            .ToList());

    public static OrderSummaryDto ToSummaryDto(this Order order) => new(
        order.Id, order.Code, order.Status.ToString(), order.Total, order.Currency, order.CreatedDate, order.Lines.Count);
}
