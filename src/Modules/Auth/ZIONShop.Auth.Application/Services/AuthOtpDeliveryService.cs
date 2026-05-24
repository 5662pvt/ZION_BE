using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Enums;
using ZIONShop.Auth.Domain.Repositories;

namespace ZIONShop.Auth.Application.Services;

public class AuthOtpDeliveryService
{
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(15);

    private readonly IAuthOtpRepository _otps;
    private readonly IEmailSender _email;
    private readonly IOtpHasher _hasher;

    public AuthOtpDeliveryService(IAuthOtpRepository otps, IEmailSender email, IOtpHasher hasher)
    {
        _otps = otps;
        _email = email;
        _hasher = hasher;
    }

    public async Task<string> SendAsync(User user, AuthOtpPurpose purpose, string subject, string bodyIntro, CancellationToken cancellationToken)
    {
        await _otps.InvalidateActiveByUserAndPurposeAsync(user.Id, purpose, cancellationToken);

        var code = _hasher.GenerateCode();
        var hash = _hasher.Hash(code);
        var otp = AuthOtp.Create(user.Id, user.Email, purpose, hash, DateTime.UtcNow.Add(OtpLifetime));
        await _otps.AddAsync(otp, cancellationToken);

        var html = $"""
            <p>{bodyIntro}</p>
            <p style="font-size:24px;font-weight:bold;letter-spacing:4px">{code}</p>
            <p>This code expires in 15 minutes.</p>
            <p>If you did not request this, you can ignore this email.</p>
            """;

        await _email.SendAsync(user.Email, subject, html, cancellationToken);
        return code;
    }
}
