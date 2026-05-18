using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Domain.Exceptions;

public static class AuthErrors
{
    public static readonly Error EmailAlreadyExists = Error.Conflict("Auth.EmailAlreadyExists", "Email already registered");
    public static readonly Error InvalidCredentials = Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password");
    public static readonly Error UserNotFound = Error.NotFound("Auth.UserNotFound", "User not found");
    public static readonly Error UserInactive = Error.Forbidden("Auth.UserInactive", "User account is inactive");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "Refresh token is invalid or expired");
}
