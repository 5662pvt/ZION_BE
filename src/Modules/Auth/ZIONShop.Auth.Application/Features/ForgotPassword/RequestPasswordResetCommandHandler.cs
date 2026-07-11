using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Application.Services;
using ZIONShop.Auth.Domain.Enums;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ForgotPassword;

public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, Result<MessageDto>>
{
    private const string GenericMessage = "If an account exists for this email, a reset code has been sent.";

    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;
    private readonly AuthOtpDeliveryService _otpDelivery;
    private readonly IDevOtpAccessor _devOtp;

    public RequestPasswordResetCommandHandler(
        IUserRepository users,
        IAuthUnitOfWork uow,
        AuthOtpDeliveryService otpDelivery,
        IDevOtpAccessor devOtp)
    {
        _users = users;
        _uow = uow;
        _otpDelivery = otpDelivery;
        _devOtp = devOtp;
    }

    public async Task<Result<MessageDto>> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _users.GetByEmailForUpdateAsync(email, cancellationToken);
        if (user is null)
            return Result.Success(new MessageDto(GenericMessage));

        var code = await _otpDelivery.SendAsync(
            user,
            AuthOtpPurpose.PasswordReset,
            "Reset your ZIONShop password",
            "Your password reset code is:",
            cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new MessageDto(GenericMessage, _devOtp.RevealIfDevelopment(code)));
    }
}
