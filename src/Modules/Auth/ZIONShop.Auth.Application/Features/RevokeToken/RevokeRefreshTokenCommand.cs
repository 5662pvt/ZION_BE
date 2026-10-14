using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.RevokeToken;

public record RevokeRefreshTokenCommand(string RefreshToken) : IRequest<Result>;
