namespace ZIONShop.Users.Application.DTOs;

public record UserProfileDto(Guid Id, Guid AuthUserId, string Email, string? FullName, string? PhoneNumber, DateTime? DateOfBirth, IReadOnlyList<AddressDto> Addresses);

public record AddressDto(Guid Id, string RecipientName, string PhoneNumber, string Province, string Ward, string StreetAddress, bool IsDefault);

public record WishlistItemDto(Guid ProductId, string Name, string Slug, decimal Price, string Currency, string? ThumbnailUrl, bool InStock, DateTime AddedAt);

public record WishlistToggleDto(Guid ProductId, bool InWishlist);

public record ProvinceDto(Guid Id, string Code, string Name);

public record WardDto(Guid Id, string Code, string Name);
