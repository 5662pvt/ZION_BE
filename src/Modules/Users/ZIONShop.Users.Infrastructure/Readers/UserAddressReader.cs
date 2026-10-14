using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Users;
using ZIONShop.Users.Infrastructure.Persistence;

namespace ZIONShop.Users.Infrastructure.Readers;

public class UserAddressReader : IUserAddressReader
{
    private readonly UsersDbContext _db;

    public UserAddressReader(UsersDbContext db) => _db = db;

    public async Task<AddressSnapshot?> GetAsync(Guid userId, Guid addressId, CancellationToken cancellationToken = default)
    {
        var row = await _db.UserProfiles.AsNoTracking()
            .Where(p => p.AuthUserId == userId)
            .Select(p => new
            {
                Address = p.Addresses.FirstOrDefault(a => a.Id == addressId)
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (row?.Address is null) return null;

        var address = row.Address;
        return new AddressSnapshot(
            address.Id,
            address.RecipientName,
            address.PhoneNumber,
            address.Province,
            address.Ward,
            address.StreetAddress);
    }
}
