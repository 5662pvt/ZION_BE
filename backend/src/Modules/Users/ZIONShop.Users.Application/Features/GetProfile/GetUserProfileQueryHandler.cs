using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.GetProfile;

public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, Result<UserProfileDto>>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IMapper _mapper;

    public GetUserProfileQueryHandler(IUserProfileRepository profiles, IMapper mapper)
    {
        _profiles = profiles;
        _mapper = mapper;
    }

    public async Task<Result<UserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile is null) return Result.Failure<UserProfileDto>(UsersErrors.ProfileNotFound);
        return Result.Success(_mapper.Map<UserProfileDto>(profile));
    }
}
