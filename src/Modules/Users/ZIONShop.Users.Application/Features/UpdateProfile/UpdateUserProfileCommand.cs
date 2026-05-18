using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.UpdateProfile;

public record UpdateUserProfileCommand(Guid AuthUserId, string? FullName, string? PhoneNumber, DateTime? DateOfBirth) : IRequest<Result<UserProfileDto>>;
