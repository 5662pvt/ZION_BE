using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.VerifyEmail;

public record VerifyEmailCommand(string Email, string Code) : IRequest<Result<AuthTokenDto>>;
