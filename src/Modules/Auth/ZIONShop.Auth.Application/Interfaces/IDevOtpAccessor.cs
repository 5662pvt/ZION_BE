namespace ZIONShop.Auth.Application.Interfaces;

/// <summary>
/// Returns the OTP for API responses in development only (never in production).
/// </summary>
public interface IDevOtpAccessor
{
    string? RevealIfDevelopment(string otp);
}
