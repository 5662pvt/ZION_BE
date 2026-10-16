using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.Contracts.Users;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.GetCustomer;

public class GetCustomerQueryHandler : IRequestHandler<GetCustomerQuery, Result<AdminCustomerDetailDto>>
{
    private readonly IUserRepository _users;
    private readonly IUserProfileReader _profiles;

    public GetCustomerQueryHandler(IUserRepository users, IUserProfileReader profiles)
    {
        _users = users;
        _profiles = profiles;
    }

    public async Task<Result<AdminCustomerDetailDto>> Handle(GetCustomerQuery request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.CustomerId, cancellationToken);
        if (user is null) return Result.Failure<AdminCustomerDetailDto>(AuthErrors.CustomerNotFound);

        var profiles = await _profiles.GetProfilesAsync(new[] { request.CustomerId }, cancellationToken);
        var profile = profiles.FirstOrDefault();

        return Result.Success(new AdminCustomerDetailDto(
            user.Id,
            user.Email,
            user.DisplayName,
            profile?.PhoneNumber,
            user.Roles.ToList(),
            user.IsActive,
            user.EmailConfirmed,
            user.CreatedDate,
            user.LastLoginAt,
            profile?.AddressesCount ?? 0));
    }
}
