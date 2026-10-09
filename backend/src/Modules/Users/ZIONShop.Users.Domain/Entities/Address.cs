using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Users.Domain.Entities;

public class Address : BaseEntity
{
    private Address() { }

    public Guid UserProfileId { get; private set; }
    public string RecipientName { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string Province { get; private set; } = string.Empty;
    public string Ward { get; private set; } = string.Empty;
    public string StreetAddress { get; private set; } = string.Empty;
    public bool IsDefault { get; private set; }

    public static Address Create(Guid userProfileId, string recipientName, string phoneNumber, string province, string ward, string streetAddress, bool isDefault) => new()
    {
        Id = Guid.NewGuid(),
        UserProfileId = userProfileId,
        RecipientName = recipientName.Trim(),
        PhoneNumber = NormalizePhone(phoneNumber),
        Province = province.Trim(),
        Ward = ward.Trim(),
        StreetAddress = streetAddress.Trim(),
        IsDefault = isDefault
    };

    public void Update(string recipientName, string phoneNumber, string province, string ward, string streetAddress)
    {
        RecipientName = recipientName.Trim();
        PhoneNumber = NormalizePhone(phoneNumber);
        Province = province.Trim();
        Ward = ward.Trim();
        StreetAddress = streetAddress.Trim();
    }

    public void SetDefault() => IsDefault = true;

    public void UnsetDefault() => IsDefault = false;

    private static string NormalizePhone(string phoneNumber) =>
        phoneNumber.Replace(" ", "").Replace(".", "").Replace("-", "").Trim();
}
