using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.GetProfile;

public record GetUserProfileQuery(Guid AuthUserId, string Email) : IRequest<Result<UserProfileDto>>;
