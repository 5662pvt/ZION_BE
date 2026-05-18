using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.AddAddress;

public class AddAddressCommandHandler : IRequestHandler<AddAddressCommand, Result<AddressDto>>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IUsersUnitOfWork _uow;
    private readonly IMapper _mapper;

    public AddAddressCommandHandler(IUserProfileRepository profiles, IUsersUnitOfWork uow, IMapper mapper)
    {
        _profiles = profiles;
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<Result<AddressDto>> Handle(AddAddressCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile is null) return Result.Failure<AddressDto>(UsersErrors.ProfileNotFound);

        var address = profile.AddAddress(request.Line1, request.Line2, request.City, request.State, request.Country, request.PostalCode, request.IsDefault);
        _profiles.Update(profile);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(_mapper.Map<AddressDto>(address));
    }
}
