using MediatR;
using ZIONShop.Auth.Application.DTOs;
using ZIONShop.Auth.Application.Interfaces;
using ZIONShop.Auth.Domain.Exceptions;
using ZIONShop.Auth.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Auth.Application.Features.LockCustomer;

public class LockCustomerCommandHandler : IRequestHandler<LockCustomerCommand, Result<CustomerLockStatusDto>>
{
    private readonly IUserRepository _users;
    private readonly IAuthUnitOfWork _uow;

    public LockCustomerCommandHandler(IUserRepository users, IAuthUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    public async Task<Result<CustomerLockStatusDto>> Handle(LockCustomerCommand request, CancellationToken cancellationToken)
    {
        if (request.CustomerId == request.ActingUserId)
            return Result.Failure<CustomerLockStatusDto>(AuthErrors.CannotLockSelf);

        var user = await _users.GetByIdAsync(request.CustomerId, cancellationToken);
        if (user is null) return Result.Failure<CustomerLockStatusDto>(AuthErrors.CustomerNotFound);

        user.Deactivate();
        user.RevokeAllRefreshTokens();
        _users.Update(user);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(new CustomerLockStatusDto(user.Id, user.IsActive));
    }
}
