using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ChangePassword;

public record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : IRequest<Result<AuthTokenDto>>;
