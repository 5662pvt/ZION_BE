using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.Login;

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthTokenDto>>;
