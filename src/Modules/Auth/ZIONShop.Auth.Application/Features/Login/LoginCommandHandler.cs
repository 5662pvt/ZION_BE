using MediatR;
using Microsoft.Extensions.Options;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Auth.Jwt;
using ZIONShop.SharedKernel.Results;
using DomainRefreshToken = ZIONShop.Auth.Domain.Entities.RefreshToken;

namespace ZIONShop.Auth.Application.Features.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthTokenDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly JwtOptions _jwtOptions;

    public LoginCommandHandler(IUserRepository users, IAuthUnitOfWork uow, IJwtTokenService jwt, IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _uow = uow;
        _jwt = jwt;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<AuthTokenDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByEmailAsync(request.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null) return Result.Failure<AuthTokenDto>(AuthErrors.InvalidCredentials);
        if (!user.IsActive) return Result.Failure<AuthTokenDto>(AuthErrors.UserInactive);
        if (!user.EmailConfirmed) return Result.Failure<AuthTokenDto>(AuthErrors.EmailNotConfirmed);
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result.Failure<AuthTokenDto>(AuthErrors.InvalidCredentials);

        var refresh = _jwt.GenerateRefreshToken();
        var refreshHash = _jwt.HashRefreshToken(refresh);
        await _users.AddRefreshTokenAsync(
            DomainRefreshToken.Create(user.Id, refreshHash, DateTime.UtcNow.AddDays(_jwtOptions.RefreshDays)),
            cancellationToken);
        await _users.SetLastLoginAsync(user.Id, DateTime.UtcNow, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var token = _jwt.GenerateAccessToken(user.Id, user.Email, user.Roles);
        return Result.Success(new AuthTokenDto(token.Token, token.ExpiresAt, refresh, user.Id, user.Email, user.Roles.ToList()));
    }
}
