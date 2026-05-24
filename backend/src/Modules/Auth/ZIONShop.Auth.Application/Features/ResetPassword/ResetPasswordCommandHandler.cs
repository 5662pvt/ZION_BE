using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Domain.Enums;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, Result<MessageDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthOtpRepository _otps;
    private readonly IAuthUnitOfWork _uow;
    private readonly IOtpHasher _hasher;

    public ResetPasswordCommandHandler(IUserRepository users, IAuthOtpRepository otps, IAuthUnitOfWork uow, IOtpHasher hasher)
    {
        _users = users;
        _otps = otps;
        _uow = uow;
        _hasher = hasher;
    }

    public async Task<Result<MessageDto>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByIdAsync(
            (await _users.GetByEmailAsync(email, cancellationToken))?.Id ?? Guid.Empty,
            cancellationToken);
        if (user is null) return Result.Failure<MessageDto>(AuthErrors.InvalidOtp);
        if (!user.EmailConfirmed) return Result.Failure<MessageDto>(AuthErrors.EmailNotConfirmed);

        var otp = await _otps.GetActiveByEmailAndPurposeAsync(email, AuthOtpPurpose.PasswordReset, cancellationToken);
        if (otp is null) return Result.Failure<MessageDto>(AuthErrors.OtpExpired);

        var hash = _hasher.Hash(request.Code.Trim());
        if (!otp.Verify(hash)) return Result.Failure<MessageDto>(AuthErrors.InvalidOtp);

        otp.MarkUsed();
        user.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
        user.RevokeAllRefreshTokens();
        _users.Update(user);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new MessageDto("Password has been reset. You can sign in with your new password."));
    }
}
