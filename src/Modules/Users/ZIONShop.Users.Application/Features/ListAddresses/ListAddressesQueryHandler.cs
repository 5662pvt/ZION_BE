using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.ListAddresses;

public class ListAddressesQueryHandler : IRequestHandler<ListAddressesQuery, Result<IReadOnlyList<AddressDto>>>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IMapper _mapper;

    public ListAddressesQueryHandler(IUserProfileRepository profiles, IMapper mapper)
    {
        _profiles = profiles;
        _mapper = mapper;
    }

    public async Task<Result<IReadOnlyList<AddressDto>>> Handle(ListAddressesQuery request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile is null) return Result.Failure<IReadOnlyList<AddressDto>>(UsersErrors.ProfileNotFound);
        IReadOnlyList<AddressDto> mapped = profile.Addresses.Select(a => _mapper.Map<AddressDto>(a)).ToList();
        return Result.Success(mapped);
    }
}
