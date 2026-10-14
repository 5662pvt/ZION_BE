using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.SetDefaultAddress;

public class SetDefaultAddressCommandHandler : IRequestHandler<SetDefaultAddressCommand, Result<AddressDto>>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IUsersUnitOfWork _uow;
    private readonly IMapper _mapper;

    public SetDefaultAddressCommandHandler(IUserProfileRepository profiles, IUsersUnitOfWork uow, IMapper mapper)
    {
        _profiles = profiles;
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<Result<AddressDto>> Handle(SetDefaultAddressCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile is null) return Result.Failure<AddressDto>(UsersErrors.ProfileNotFound);

        if (!profile.SetDefaultAddress(request.AddressId))
            return Result.Failure<AddressDto>(UsersErrors.AddressNotFound);

        var address = profile.Addresses.Single(a => a.Id == request.AddressId);
        _profiles.Update(profile);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(_mapper.Map<AddressDto>(address));
    }
}
