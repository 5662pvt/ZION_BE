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

namespace ZIONShop.Auth.Application.Features.RefreshToken;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthTokenDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly JwtOptions _jwtOptions;

    public RefreshTokenCommandHandler(IUserRepository users, IAuthUnitOfWork uow, IJwtTokenService jwt, IOptions<JwtOptions> jwtOptions)
    {
        _users = users;
        _uow = uow;
        _jwt = jwt;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<Result<AuthTokenDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = _jwt.HashRefreshToken(request.RefreshToken);
        var user = await _users.GetByRefreshTokenHashAsync(hash, cancellationToken);
        if (user is null) return Result.Failure<AuthTokenDto>(AuthErrors.InvalidRefreshToken);

        var existing = user.RefreshTokens.FirstOrDefault(t => t.TokenHash == hash);
        if (existing is null || !existing.IsActive) return Result.Failure<AuthTokenDto>(AuthErrors.InvalidRefreshToken);

        existing.Revoke();

        var newRefresh = _jwt.GenerateRefreshToken();
        var newHash = _jwt.HashRefreshToken(newRefresh);
        await _users.AddRefreshTokenAsync(
            DomainRefreshToken.Create(user.Id, newHash, DateTime.UtcNow.AddDays(_jwtOptions.RefreshDays)),
            cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        var token = _jwt.GenerateAccessToken(user.Id, user.Email, user.Roles);
        return Result.Success(new AuthTokenDto(token.Token, token.ExpiresAt, newRefresh, user.Id, user.Email, user.Roles.ToList()));
    }
}
