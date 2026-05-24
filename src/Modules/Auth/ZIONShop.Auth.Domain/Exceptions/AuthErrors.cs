using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Domain.Exceptions;

public static class AuthErrors
{
    public static readonly Error EmailAlreadyExists = Error.Conflict("Auth.EmailAlreadyExists", "Email already registered");
    public static readonly Error InvalidCredentials = Error.Unauthorized("Auth.InvalidCredentials", "Invalid email or password");
    public static readonly Error UserNotFound = Error.NotFound("Auth.UserNotFound", "User not found");
    public static readonly Error UserInactive = Error.Forbidden("Auth.UserInactive", "User account is inactive");
    public static readonly Error InvalidRefreshToken = Error.Unauthorized("Auth.InvalidRefreshToken", "Refresh token is invalid or expired");
    public static readonly Error EmailNotConfirmed = Error.Forbidden("Auth.EmailNotConfirmed", "Email address is not verified");
    public static readonly Error EmailAlreadyConfirmed = Error.Conflict("Auth.EmailAlreadyConfirmed", "Email is already verified");
    public static readonly Error InvalidOtp = Error.Validation("Auth.InvalidOtp", "Verification code is invalid");
    public static readonly Error OtpExpired = Error.Validation("Auth.OtpExpired", "Verification code has expired");
}
