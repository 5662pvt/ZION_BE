using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Application.Services;
using ZIONShop.Auth.Domain.Enums;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ResendVerification;

public class ResendVerificationCommandHandler : IRequestHandler<ResendVerificationCommand, Result<MessageDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;
    private readonly AuthOtpDeliveryService _otpDelivery;

    public ResendVerificationCommandHandler(IUserRepository users, IAuthUnitOfWork uow, AuthOtpDeliveryService otpDelivery)
    {
        _users = users;
        _uow = uow;
        _otpDelivery = otpDelivery;
    }

    public async Task<Result<MessageDto>> Handle(ResendVerificationCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailForUpdateAsync(email, cancellationToken);
        if (user is null)
            return Result.Success(new MessageDto("If the account exists, a verification code has been sent."));

        if (user.EmailConfirmed)
            return Result.Failure<MessageDto>(AuthErrors.EmailAlreadyConfirmed);

        await _otpDelivery.SendAsync(
            user,
            AuthOtpPurpose.EmailVerification,
            "Verify your ZIONShop account",
            "Your email verification code is:",
            cancellationToken);

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(new MessageDto("Verification code sent."));
    }
}
