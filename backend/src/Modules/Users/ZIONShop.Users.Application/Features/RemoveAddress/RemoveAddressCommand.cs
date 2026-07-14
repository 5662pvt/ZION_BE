using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Users.Application.Features.RemoveAddress;

public record RemoveAddressCommand(Guid AuthUserId, Guid AddressId) : IRequest<Result>;
