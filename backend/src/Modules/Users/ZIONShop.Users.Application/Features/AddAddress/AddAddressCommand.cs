using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.AddAddress;

public record AddAddressCommand(
    Guid AuthUserId,
    string Line1,
    string? Line2,
    string City,
    string? State,
    string Country,
    string PostalCode,
    bool IsDefault) : IRequest<Result<AddressDto>>;
