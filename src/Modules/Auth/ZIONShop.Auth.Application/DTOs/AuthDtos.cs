namespace ZIONShop.Auth.Application.DTOs;

public record AuthTokenDto(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, Guid UserId, string Email, IReadOnlyList<string> Roles);

public record RegisterPendingDto(string Email, bool RequiresVerification);

public record MessageDto(string Message);

public record UserDto(Guid Id, string Email, string? DisplayName, IReadOnlyList<string> Roles);

public record AdminCustomerDto(
    Guid Id,
    string Email,
    string? DisplayName,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool EmailConfirmed,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

public record AdminCustomerDetailDto(
    Guid Id,
    string Email,
    string? DisplayName,
    string? PhoneNumber,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool EmailConfirmed,
    DateTime CreatedAt,
    DateTime? LastLoginAt,
    int AddressesCount);

public record CustomerLockStatusDto(Guid Id, bool IsActive);
