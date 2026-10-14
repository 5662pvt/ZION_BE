using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Application.Services;
using ZIONShop.Auth.Domain.Enums;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.VerifyEmail;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, Result<AuthTokenDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthOtpRepository _otps;
    private readonly IAuthUnitOfWork _uow;
    private readonly IOtpHasher _hasher;
    private readonly AuthTokenIssuer _tokenIssuer;

    public VerifyEmailCommandHandler(
        IUserRepository users,
        IAuthOtpRepository otps,
        IAuthUnitOfWork uow,
        IOtpHasher hasher,
        AuthTokenIssuer tokenIssuer)
    {
        _users = users;
        _otps = otps;
        _uow = uow;
        _hasher = hasher;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<Result<AuthTokenDto>> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailForUpdateAsync(email, cancellationToken);
        if (user is null) return Result.Failure<AuthTokenDto>(AuthErrors.InvalidOtp);

        if (user.EmailConfirmed)
            return Result.Failure<AuthTokenDto>(AuthErrors.EmailAlreadyConfirmed);

        var otp = await _otps.GetActiveByEmailAndPurposeAsync(email, AuthOtpPurpose.EmailVerification, cancellationToken);
        if (otp is null) return Result.Failure<AuthTokenDto>(AuthErrors.OtpExpired);

        var hash = _hasher.Hash(request.Code.Trim());
        if (!otp.Verify(hash)) return Result.Failure<AuthTokenDto>(AuthErrors.InvalidOtp);

        otp.MarkUsed();
        user.ConfirmEmail();
        _users.Update(user);
        await _uow.SaveChangesAsync(cancellationToken);

        var tokens = await _tokenIssuer.IssueAsync(user, cancellationToken);
        return Result.Success(tokens);
    }
}
