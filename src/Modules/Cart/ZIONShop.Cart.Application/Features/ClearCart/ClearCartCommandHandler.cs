using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Application.Mappings;
using ZIONShop.Cart.Domain.Exceptions;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.SharedKernel.Results;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Application.Features.ClearCart;

public class ClearCartCommandHandler : IRequestHandler<ClearCartCommand, Result<CartDto>>
{
    private readonly ICartRepository _carts;
    private readonly ICartUnitOfWork _uow;

    public ClearCartCommandHandler(ICartRepository carts, ICartUnitOfWork uow)
    {
        _carts = carts;
        _uow = uow;
    }

    public async Task<Result<CartDto>> Handle(ClearCartCommand request, CancellationToken cancellationToken)
    {
        DomainCart? cart = request.UserId.HasValue
            ? await _carts.GetByUserIdAsync(request.UserId.Value, cancellationToken)
            : await _carts.GetByAnonymousIdAsync(request.AnonymousId ?? string.Empty, cancellationToken);
        if (cart is null) return Result.Failure<CartDto>(CartErrors.CartNotFound);
        cart.Clear();
        _carts.Update(cart);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(cart.ToDto());
    }
}
