using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Application.Services;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.AddAddress;

public class AddAddressCommandHandler : IRequestHandler<AddAddressCommand, Result<AddressDto>>
{
    private readonly IUserProfileProvisioner _provisioner;
    private readonly IUserProfileRepository _profiles;
    private readonly IAdministrativeUnitRepository _administrativeUnits;
    private readonly IUsersUnitOfWork _uow;
    private readonly IMapper _mapper;

    public AddAddressCommandHandler(
        IUserProfileProvisioner provisioner,
        IUserProfileRepository profiles,
        IAdministrativeUnitRepository administrativeUnits,
        IUsersUnitOfWork uow,
        IMapper mapper)
    {
        _provisioner = provisioner;
        _profiles = profiles;
        _administrativeUnits = administrativeUnits;
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<Result<AddressDto>> Handle(AddAddressCommand request, CancellationToken cancellationToken)
    {
        var province = await _administrativeUnits.FindProvinceByNameAsync(request.Province, cancellationToken);
        if (province is null) return Result.Failure<AddressDto>(UsersErrors.InvalidProvince);

        var ward = await _administrativeUnits.FindWardByNameAsync(province.Id, request.Ward, cancellationToken);
        if (ward is null) return Result.Failure<AddressDto>(UsersErrors.InvalidWard);

        var profile = await _provisioner.EnsureAsync(request.AuthUserId, request.Email, cancellationToken: cancellationToken);

        var address = profile.AddAddress(request.RecipientName, request.PhoneNumber, province.Name, ward.Name, request.StreetAddress, request.IsDefault);
        await _profiles.AddAddressAsync(address, cancellationToken);
        _profiles.Update(profile);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(_mapper.Map<AddressDto>(address));
    }
}
