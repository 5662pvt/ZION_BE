using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.Me;

public record GetCurrentUserQuery(Guid UserId) : IRequest<Result<UserDto>>;
