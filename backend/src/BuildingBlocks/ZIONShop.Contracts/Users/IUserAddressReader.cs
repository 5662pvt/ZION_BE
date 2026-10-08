namespace ZIONShop.Contracts.Users;

public record AddressSnapshot(
    Guid AddressId,
    string RecipientName,
    string? Phone,
    string Line1,
    string? Line2,
    string City,
    string? State,
    string Country,
    string PostalCode);

public interface IUserAddressReader
{
    Task<AddressSnapshot?> GetAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default);
}
