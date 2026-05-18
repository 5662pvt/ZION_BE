using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.Register;

public record RegisterUserCommand(string Email, string Password, string? DisplayName) : IRequest<Result<AuthTokenDto>>;
