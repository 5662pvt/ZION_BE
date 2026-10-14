using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.RemoveAddress;

public class RemoveAddressCommandHandler : IRequestHandler<RemoveAddressCommand, Result>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IUsersUnitOfWork _uow;

    public RemoveAddressCommandHandler(IUserProfileRepository profiles, IUsersUnitOfWork uow)
    {
        _profiles = profiles;
        _uow = uow;
    }

    public async Task<Result> Handle(RemoveAddressCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile is null) return Result.Failure(UsersErrors.ProfileNotFound);

        if (!profile.RemoveAddress(request.AddressId))
            return Result.Failure(UsersErrors.AddressNotFound);

        _profiles.Update(profile);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
