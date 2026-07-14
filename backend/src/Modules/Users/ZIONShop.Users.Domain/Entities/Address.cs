using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Users.Domain.Entities;

public class Address : BaseEntity
{
    private Address() { }

    public Guid UserProfileId { get; private set; }
    public string Line1 { get; private set; } = string.Empty;
    public string? Line2 { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? State { get; private set; }
    public string Country { get; private set; } = string.Empty;
    public string PostalCode { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    public static Address Create(Guid userProfileId, string line1, string? line2, string city, string? state, string country, string postalCode, bool isDefault) => new()
    {
        Id = Guid.NewGuid(),
        UserProfileId = userProfileId,
        Line1 = line1.Trim(),
        Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim(),
        City = city.Trim(),
        State = string.IsNullOrWhiteSpace(state) ? null : state.Trim(),
        Country = country.Trim(),
        PostalCode = postalCode.Trim(),
        IsDefault = isDefault
    };

    public void Update(string line1, string? line2, string city, string? state, string country, string postalCode)
    {
        Line1 = line1.Trim();
        Line2 = string.IsNullOrWhiteSpace(line2) ? null : line2.Trim();
        City = city.Trim();
        State = string.IsNullOrWhiteSpace(state) ? null : state.Trim();
        Country = country.Trim();
        PostalCode = postalCode.Trim();
    }

    public void SetDefault() => IsDefault = true;

    public void UnsetDefault() => IsDefault = false;
}
