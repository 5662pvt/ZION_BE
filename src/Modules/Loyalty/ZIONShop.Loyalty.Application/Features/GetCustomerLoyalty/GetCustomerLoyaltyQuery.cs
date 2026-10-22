using MediatR;
using ZIONShop.Loyalty.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Loyalty.Application.Features.GetCustomerLoyalty;

public record GetCustomerLoyaltyQuery(Guid UserId) : IRequest<Result<CustomerLoyaltyDto>>;
