using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthTokenDto>>;
