using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.UnlockCustomer;

public class UnlockCustomerCommandHandler : IRequestHandler<UnlockCustomerCommand, Result<CustomerLockStatusDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;

    public UnlockCustomerCommandHandler(IUserRepository users, IAuthUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<CustomerLockStatusDto>> Handle(UnlockCustomerCommand request, CancellationToken cancellationToken)
    {
        var user = await _users.GetByIdAsync(request.CustomerId, cancellationToken);
        if (user is null) return Result.Failure<CustomerLockStatusDto>(AuthErrors.CustomerNotFound);

        user.Activate();
        _users.Update(user);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new CustomerLockStatusDto(user.Id, user.IsActive));
    }
}
