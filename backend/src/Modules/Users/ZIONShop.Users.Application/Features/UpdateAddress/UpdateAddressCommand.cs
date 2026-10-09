using MediatR;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.UpdateAddress;

public record UpdateAddressCommand(
    Guid AuthUserId,
    Guid AddressId,
    string RecipientName,
    string PhoneNumber,
    string Province,
    string Ward,
    string StreetAddress,
    bool IsDefault) : IRequest<Result<AddressDto>>;
