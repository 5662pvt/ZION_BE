namespace ZIONShop.Auth.Application.DTOs;

public record AuthTokenDto(string AccessToken, DateTime AccessTokenExpiresAt, string RefreshToken, Guid UserId, string Email, IReadOnlyList<string> Roles);

public record UserDto(Guid Id, string Email, string? DisplayName, IReadOnlyList<string> Roles);
