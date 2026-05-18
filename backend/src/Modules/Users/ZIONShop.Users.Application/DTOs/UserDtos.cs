namespace ZIONShop.Users.Application.DTOs;

public record UserProfileDto(Guid Id, Guid AuthUserId, string Email, string? FullName, string? PhoneNumber, DateTime? DateOfBirth, IReadOnlyList<AddressDto> Addresses);

public record AddressDto(Guid Id, string Line1, string? Line2, string City, string? State, string Country, string PostalCode, bool IsDefault);
