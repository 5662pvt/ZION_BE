using ZIONShop.Contracts.Users;

namespace ZIONShop.Orders.Tests.Fakes;

public class FakeUserAddressReader : IUserAddressReader
{
    private readonly Dictionary<(Guid UserId, Guid AddressId), AddressSnapshot> _addresses = new();

    public FakeUserAddressReader WithAddress(Guid userId, Guid addressId, string recipientName = "Nguyen Van A")
    {
        _addresses[(userId, addressId)] = new AddressSnapshot(addressId, recipientName, "0900000000", "Hà Nội", "Cầu Giấy", "123 Đường ABC");
        return this;
    }

    public Task<AddressSnapshot?> GetAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default)
        => Task.FromResult(_addresses.TryGetValue((userId, addressId), out var address) ? address : null);
}
