using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Application.Services;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result<AuthTokenDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;
    private readonly AuthTokenIssuer _tokenIssuer;

    public ChangePasswordCommandHandler(IUserRepository users, IAuthUnitOfWork uow, AuthTokenIssuer tokenIssuer)
    {
        _users = users;
        _uow = uow;
        _tokenIssuer = tokenIssuer;
    }

    public async Task<Result<AuthTokenDto>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return Result.Failure<AuthTokenDto>(AuthErrors.UserNotFound);

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            return Result.Failure<AuthTokenDto>(AuthErrors.InvalidCurrentPassword);

        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
            return Result.Failure<AuthTokenDto>(AuthErrors.PasswordUnchanged);

        user.SetPasswordHash(BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
        user.RevokeAllRefreshTokens();
        _users.Update(user);
        await _uow.SaveChangesAsync(cancellationToken);

        var tokens = await _tokenIssuer.IssueAsync(user, cancellationToken);
        return Result.Success(tokens);
    }
}
