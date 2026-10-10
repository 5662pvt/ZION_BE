using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Application.Mappings;
using ZIONShop.Cart.Domain.Exceptions;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.Contracts.Products;
using ZIONShop.SharedKernel.Results;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Application.Features.UpdateItem;

public class UpdateCartItemCommandHandler : IRequestHandler<UpdateCartItemCommand, Result<CartDto>>
{
    private readonly ICartRepository _carts;
    private readonly ICartUnitOfWork _uow;
    private readonly IProductPriceLookup _prices;

    public UpdateCartItemCommandHandler(ICartRepository carts, ICartUnitOfWork uow, IProductPriceLookup prices)
    {
        _carts = carts;
        _uow = uow;
        _prices = prices;
    }

    public async Task<Result<CartDto>> Handle(UpdateCartItemCommand request, CancellationToken cancellationToken)
    {
        var cart = await ResolveCartAsync(request.UserId, request.AnonymousId, cancellationToken);
        if (cart is null) return Result.Failure<CartDto>(CartErrors.CartNotFound);

        var updated = cart.UpdateItem(request.ItemId, request.Quantity);
        if (updated is null) return Result.Failure<CartDto>(CartErrors.ItemNotFound);

        _carts.Update(cart);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(await cart.ToEnrichedDtoAsync(_prices, cancellationToken));
    }

    private async Task<DomainCart?> ResolveCartAsync(Guid? userId, string? anonymousId, CancellationToken cancellationToken)
    {
        if (userId.HasValue) return await _carts.GetByUserIdAsync(userId.Value, cancellationToken);
        if (!string.IsNullOrWhiteSpace(anonymousId)) return await _carts.GetByAnonymousIdAsync(anonymousId, cancellationToken);
        return null;
    }
}
