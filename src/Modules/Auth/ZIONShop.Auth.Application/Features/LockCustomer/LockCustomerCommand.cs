using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.LockCustomer;

public record LockCustomerCommand(Guid CustomerId, Guid ActingUserId) : IRequest<Result<CustomerLockStatusDto>>;
