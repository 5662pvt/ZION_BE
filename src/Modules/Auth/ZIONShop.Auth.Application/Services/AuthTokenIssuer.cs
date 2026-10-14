using Microsoft.Extensions.Options;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Domain.Entities;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Auth.Jwt;
using DomainRefreshToken = ZIONShop.Auth.Domain.Entities.RefreshToken;

namespace ZIONShop.Auth.Application.Services;

public class AuthTokenIssuer
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly JwtOptions _jwtOptions;

    public AuthTokenIssuer(
        IUserRepository users,
        IAuthUnitOfWork uow,
        IJwtTokenService jwt,
        IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _uow = uow;
        _jwt = jwt;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthTokenDto> IssueAsync(User user, CancellationToken cancellationToken)
    {
        var refresh = _jwt.GenerateRefreshToken();
        var refreshHash = _jwt.HashRefreshToken(refresh);
        await _users.AddRefreshTokenAsync(
            DomainRefreshToken.Create(user.Id, refreshHash, DateTime.UtcNow.AddDays(_jwtOptions.RefreshDays)),
            cancellationToken);
        await _users.SetLastLoginAsync(user.Id, DateTime.UtcNow, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var token = _jwt.GenerateAccessToken(user.Id, user.Email, user.Roles);
        return new AuthTokenDto(token.Token, token.ExpiresAt, refresh, user.Id, user.Email, user.Roles.ToList());
    }
}
