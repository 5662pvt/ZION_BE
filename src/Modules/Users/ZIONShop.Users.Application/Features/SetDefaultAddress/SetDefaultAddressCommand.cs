using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.SetDefaultAddress;

public record SetDefaultAddressCommand(Guid AuthUserId, Guid AddressId) : IRequest<Result<AddressDto>>;
