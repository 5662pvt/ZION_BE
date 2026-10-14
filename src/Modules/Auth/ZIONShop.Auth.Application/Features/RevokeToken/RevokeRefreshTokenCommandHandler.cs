using MediatR;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Auth.Jwt;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.RevokeToken;

public class RevokeRefreshTokenCommandHandler : IRequestHandler<RevokeRefreshTokenCommand, Result>
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;

    public RevokeRefreshTokenCommandHandler(IUserRepository users, IAuthUnitOfWork uow, IJwtTokenService jwt)
    {
        _users = users;
        _uow = uow;
        _jwt = jwt;
    }

    public async Task<Result> Handle(RevokeRefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var hash = _jwt.HashRefreshToken(request.RefreshToken);
        var user = await _users.GetByRefreshTokenHashAsync(hash, cancellationToken);
        if (user is null) return Result.Success();

        user.RevokeRefreshToken(hash);
        _users.Update(user);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
