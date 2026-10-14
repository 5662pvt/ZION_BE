using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Application.Services;

namespace ZIONShop.Users.Application.Features.ListAddresses;

public class ListAddressesQueryHandler : IRequestHandler<ListAddressesQuery, Result<IReadOnlyList<AddressDto>>>
{
    private readonly IUserProfileProvisioner _provisioner;
    private readonly IMapper _mapper;

    public ListAddressesQueryHandler(IUserProfileProvisioner provisioner, IMapper mapper)
    {
        _provisioner = provisioner;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<AddressDto>>> Handle(ListAddressesQuery request, CancellationToken cancellationToken)
    {
        var profile = await _provisioner.EnsureAsync(request.AuthUserId, request.Email, cancellationToken: cancellationToken);
        IReadOnlyList<AddressDto> mapped = profile.Addresses.Select(a => _mapper.Map<AddressDto>(a)).ToList();
        return Result.Success(mapped);
    }
}
