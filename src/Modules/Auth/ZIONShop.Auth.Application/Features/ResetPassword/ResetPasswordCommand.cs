using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ResetPassword;

public record ResetPasswordCommand(string Email, string Code, string NewPassword) : IRequest<Result<MessageDto>>;
