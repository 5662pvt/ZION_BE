using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.UnlockCustomer;

public record UnlockCustomerCommand(Guid CustomerId) : IRequest<Result<CustomerLockStatusDto>>;
