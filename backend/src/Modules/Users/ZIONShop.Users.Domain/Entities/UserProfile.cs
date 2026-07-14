using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Users.Domain.Entities;

public class UserProfile : AggregateRoot
{
    private readonly List<Address> _addresses = new();

    private UserProfile() { }

    public Guid AuthUserId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? FullName { get; private set; }
    public string? PhoneNumber { get; private set; }
    public DateTime? DateOfBirth { get; private set; }

    public IReadOnlyCollection<Address> Addresses => _addresses.AsReadOnly();

    public static UserProfile Create(Guid authUserId, string email, string? fullName = null) => new()
    {
        Id = Guid.NewGuid(),
        AuthUserId = authUserId,
        Email = email,
        FullName = fullName
    };

    public void UpdateProfile(string? fullName, string? phoneNumber, DateTime? dateOfBirth)
    {
        FullName = fullName?.Trim();
        PhoneNumber = phoneNumber?.Trim();
        DateOfBirth = dateOfBirth;
    }

    public Address AddAddress(string line1, string? line2, string city, string? state, string country, string postalCode, bool isDefault)
    {
        if (isDefault)
        {
            foreach (var addr in _addresses) addr.UnsetDefault();
        }
        else if (_addresses.Count == 0)
        {
            isDefault = true;
        }
        var address = Address.Create(Id, line1, line2, city, state, country, postalCode, isDefault);
        _addresses.Add(address);
        return address;
    }

    public Address? UpdateAddress(Guid addressId, string line1, string? line2, string city, string? state, string country, string postalCode, bool isDefault)
    {
        var address = _addresses.FirstOrDefault(a => a.Id == addressId);
        if (address is null) return null;

        address.Update(line1, line2, city, state, country, postalCode);
        if (isDefault) SetDefaultAddress(addressId);
        return address;
    }

    public bool RemoveAddress(Guid addressId)
    {
        var address = _addresses.FirstOrDefault(a => a.Id == addressId);
        if (address is null) return false;

        var wasDefault = address.IsDefault;
        _addresses.Remove(address);

        if (wasDefault && _addresses.Count > 0)
            _addresses[0].SetDefault();

        return true;
    }

    public bool SetDefaultAddress(Guid addressId)
    {
        var address = _addresses.FirstOrDefault(a => a.Id == addressId);
        if (address is null) return false;

        foreach (var item in _addresses) item.UnsetDefault();
        address.SetDefault();
        return true;
    }
}
