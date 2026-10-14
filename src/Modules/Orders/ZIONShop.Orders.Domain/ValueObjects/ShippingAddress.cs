namespace ZIONShop.Orders.Domain.ValueObjects;

public sealed record ShippingAddress(
    Guid AddressId,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string Ward,
    string StreetAddress);
