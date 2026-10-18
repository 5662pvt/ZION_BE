using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.UpdateAddress;

public class UpdateAddressCommandHandler : IRequestHandler<UpdateAddressCommand, Result<AddressDto>>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IAdministrativeUnitRepository _administrativeUnits;
    private readonly IUsersUnitOfWork _uow;
    private readonly IMapper _mapper;

    public UpdateAddressCommandHandler(
        IUserProfileRepository profiles,
        IAdministrativeUnitRepository administrativeUnits,
        IUsersUnitOfWork uow,
        IMapper mapper)
    {
        _profiles = profiles;
        _administrativeUnits = administrativeUnits;
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<Result<AddressDto>> Handle(UpdateAddressCommand request, CancellationToken cancellationToken)
    {
        var province = await _administrativeUnits.FindProvinceByNameAsync(request.Province, cancellationToken);
        if (province is null) return Result.Failure<AddressDto>(UsersErrors.InvalidProvince);

        var ward = await _administrativeUnits.FindWardByNameAsync(province.Id, request.Ward, cancellationToken);
        if (ward is null) return Result.Failure<AddressDto>(UsersErrors.InvalidWard);

        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile is null) return Result.Failure<AddressDto>(UsersErrors.ProfileNotFound);

        var address = profile.UpdateAddress(
            request.AddressId,
            request.RecipientName,
            request.PhoneNumber,
            province.Name,
            ward.Name,
            request.StreetAddress,
            request.IsDefault);

        if (address is null) return Result.Failure<AddressDto>(UsersErrors.AddressNotFound);

        _profiles.Update(profile);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(_mapper.Map<AddressDto>(address));
    }
}
