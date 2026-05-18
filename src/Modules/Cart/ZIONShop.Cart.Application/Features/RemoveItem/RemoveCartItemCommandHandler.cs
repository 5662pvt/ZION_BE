using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Application.Mappings;
using ZIONShop.Cart.Domain.Exceptions;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.SharedKernel.Results;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Application.Features.RemoveItem;

public class RemoveCartItemCommandHandler : IRequestHandler<RemoveCartItemCommand, Result<CartDto>>
{
    private readonly ICartRepository _carts;
    private readonly ICartUnitOfWork _uow;

    public RemoveCartItemCommandHandler(ICartRepository carts, ICartUnitOfWork uow)
    {
        _carts = carts;
        _uow = uow;
    }

    public async Task<Result<CartDto>> Handle(RemoveCartItemCommand request, CancellationToken cancellationToken)
    {
        DomainCart? cart = request.UserId.HasValue
            ? await _carts.GetByUserIdAsync(request.UserId.Value, cancellationToken)
            : await _carts.GetByAnonymousIdAsync(request.AnonymousId ?? string.Empty, cancellationToken);
        if (cart is null) return Result.Failure<CartDto>(CartErrors.CartNotFound);

        if (!cart.RemoveItem(request.ItemId)) return Result.Failure<CartDto>(CartErrors.ItemNotFound);
        _carts.Update(cart);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(cart.ToDto());
    }
}
