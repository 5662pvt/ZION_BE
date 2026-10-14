using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.AddAddress;

public record AddAddressCommand(
    Guid AuthUserId,
    string Email,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string Ward,
    string StreetAddress,
    bool IsDefault) : IRequest<Result<AddressDto>>;
