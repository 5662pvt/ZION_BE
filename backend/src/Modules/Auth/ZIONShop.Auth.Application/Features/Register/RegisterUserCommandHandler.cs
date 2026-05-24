using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Application.Services;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Enums;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Contracts.Auth;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.Register;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<RegisterPendingDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;
    private readonly IEventBus _eventBus;
    private readonly AuthOtpDeliveryService _otpDelivery;

    public RegisterUserCommandHandler(
        IUserRepository users,
        IAuthUnitOfWork uow,
        IEventBus eventBus,
        AuthOtpDeliveryService otpDelivery)
    {
        _users = users;
        _uow = uow;
        _eventBus = eventBus;
        _otpDelivery = otpDelivery;
    }

    public async Task<Result<RegisterPendingDto>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.EmailExistsAsync(email, cancellationToken))
            return Result.Failure<RegisterPendingDto>(AuthErrors.EmailAlreadyExists);

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = User.Register(email, hash, request.DisplayName);

        await _users.AddAsync(user, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new UserRegisteredIntegrationEvent(user.Id, user.Email, user.DisplayName), cancellationToken);

        await _otpDelivery.SendAsync(
            user,
            AuthOtpPurpose.EmailVerification,
            "Verify your ZIONShop account",
            "Your email verification code is:",
            cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new RegisterPendingDto(user.Email, true));
    }
}
