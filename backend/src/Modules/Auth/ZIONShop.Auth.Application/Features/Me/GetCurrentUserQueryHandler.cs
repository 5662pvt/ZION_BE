using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.Me;

public class GetCurrentUserQueryHandler : IRequestHandler<GetCurrentUserQuery, Result<UserDto>>
{
    private readonly IUserRepository _users;

    public GetCurrentUserQueryHandler(IUserRepository users) => _users = users;

    public async Task<Result<UserDto>> Handle(GetCurrentUserQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null) return Result.Failure<UserDto>(AuthErrors.UserNotFound);
        return Result.Success(new UserDto(user.Id, user.Email, user.DisplayName, user.Roles.ToList()));
    }
}
