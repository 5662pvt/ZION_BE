using MediatR;
using Microsoft.Extensions.Options;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Auth.Jwt;
using ZIONShop.Contracts.Auth;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.SharedKernel.Results;
using DomainRefreshToken = ZIONShop.Auth.Domain.Entities.RefreshToken;

namespace ZIONShop.Auth.Application.Features.Register;

public class RegisterUserCommandHandler : IRequestHandler<RegisterUserCommand, Result<AuthTokenDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly IEventBus _eventBus;
    private readonly JwtOptions _jwtOptions;

    public RegisterUserCommandHandler(
        IUserRepository users,
        IAuthUnitOfWork uow,
        IJwtTokenService jwt,
        IEventBus eventBus,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _uow = uow;
        _jwt = jwt;
        _eventBus = eventBus;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<AuthTokenDto>> Handle(RegisterUserCommand request, CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await _users.EmailExistsAsync(email, cancellationToken))
            return Result.Failure<AuthTokenDto>(AuthErrors.EmailAlreadyExists);

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = User.Register(email, hash, request.DisplayName);

        var refresh = _jwt.GenerateRefreshToken();
        var refreshHash = _jwt.HashRefreshToken(refresh);
        user.AddRefreshToken(DomainRefreshToken.Create(user.Id, refreshHash, DateTime.UtcNow.AddDays(_jwtOptions.RefreshDays)));

        await _users.AddAsync(user, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        await _eventBus.PublishAsync(new UserRegisteredIntegrationEvent(user.Id, user.Email, user.DisplayName), cancellationToken);

        var token = _jwt.GenerateAccessToken(user.Id, user.Email, user.Roles);
        return Result.Success(new AuthTokenDto(token.Token, token.ExpiresAt, refresh, user.Id, user.Email, user.Roles.ToList()));
    }
}
