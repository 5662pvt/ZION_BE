using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.GetCustomer;

public record GetCustomerQuery(Guid CustomerId) : IRequest<Result<AdminCustomerDetailDto>>;
