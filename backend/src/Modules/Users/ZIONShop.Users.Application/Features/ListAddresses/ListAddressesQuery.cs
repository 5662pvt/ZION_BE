using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.ListAddresses;

public record ListAddressesQuery(Guid AuthUserId) : IRequest<Result<IReadOnlyList<AddressDto>>>;
