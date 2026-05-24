using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ForgotPassword;

public record RequestPasswordResetCommand(string Email) : IRequest<Result<MessageDto>>;
