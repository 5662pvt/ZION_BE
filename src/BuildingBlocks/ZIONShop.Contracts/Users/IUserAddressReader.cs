namespace ZIONShop.Contracts.Users;

public record AddressSnapshot(
    Guid AddressId,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string Ward,
    string StreetAddress);

public interface IUserAddressReader
{
    Task<AddressSnapshot?> GetAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default);
}
