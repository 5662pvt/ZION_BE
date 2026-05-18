using AutoMapper;
using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;
using ZIONShop.Users.Application.Interfaces;
using ZIONShop.Users.Domain.Exceptions;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.UpdateProfile;

public class UpdateUserProfileCommandHandler : IRequestHandler<UpdateUserProfileCommand, Result<UserProfileDto>>
{
    private readonly IUserProfileRepository _profiles;
    private readonly IUsersUnitOfWork _uow;
    private readonly IMapper _mapper;

    public UpdateUserProfileCommandHandler(IUserProfileRepository profiles, IUsersUnitOfWork uow, IMapper mapper)
    {
        _profiles = profiles;
        _uow = uow;
        _mapper = mapper;
    }

    public async Task<Result<UserProfileDto>> Handle(UpdateUserProfileCommand request, CancellationToken cancellationToken)
    {
        var profile = await _profiles.GetByAuthUserIdAsync(request.AuthUserId, cancellationToken);
        if (profile is null) return Result.Failure<UserProfileDto>(UsersErrors.ProfileNotFound);

        profile.UpdateProfile(request.FullName, request.PhoneNumber, request.DateOfBirth);
        _profiles.Update(profile);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(_mapper.Map<UserProfileDto>(profile));
    }
}
