using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.ResendVerification;

public record ResendVerificationCommand(string Email) : IRequest<Result<MessageDto>>;
