namespace ZIONShop.Users.Application.DTOs;

public record UserProfileDto(Guid Id, Guid AuthUserId, string Email, string? FullName, string? PhoneNumber, DateTime? DateOfBirth, IReadOnlyList<AddressDto> Addresses);

public record AddressDto(Guid Id, string RecipientName, string PhoneNumber, string Province, string Ward, string StreetAddress, bool IsDefault);
