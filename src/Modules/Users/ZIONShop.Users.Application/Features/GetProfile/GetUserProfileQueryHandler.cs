using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Application.Services;

namespace ZIONShop.Users.Application.Features.GetProfile;

public class GetUserProfileQueryHandler : IRequestHandler<GetUserProfileQuery, Result<UserProfileDto>>
{
    private readonly IUserProfileProvisioner _provisioner;
    private readonly IMapper _mapper;

    public GetUserProfileQueryHandler(IUserProfileProvisioner provisioner, IMapper mapper)
    {
        _provisioner = provisioner;
        _mapper = mapper;
    }

    public async Task<Result<UserProfileDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        var profile = await _provisioner.EnsureAsync(request.AuthUserId, request.Email, cancellationToken: cancellationToken);
        return Result.Success(_mapper.Map<UserProfileDto>(profile));
    }
}
