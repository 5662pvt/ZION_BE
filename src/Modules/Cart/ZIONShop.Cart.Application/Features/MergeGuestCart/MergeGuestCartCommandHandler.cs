using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Application.Mappings;
using ZIONShop.Cart.Domain.Exceptions;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.Contracts.Products;
using ZIONShop.SharedKernel.Results;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Application.Features.MergeGuestCart;

public class MergeGuestCartCommandHandler : IRequestHandler<MergeGuestCartCommand, Result<CartDto>>
{
    private readonly ICartRepository _carts;
    private readonly ICartUnitOfWork _uow;
    private readonly IProductPriceLookup _prices;

    public MergeGuestCartCommandHandler(ICartRepository carts, ICartUnitOfWork uow, IProductPriceLookup prices)
    {
        _carts = carts;
        _uow = uow;
        _prices = prices;
    }

    public async Task<Result<CartDto>> Handle(MergeGuestCartCommand request, CancellationToken cancellationToken)
    {
        var userCart = await EnsureUserCartAsync(request.UserId, cancellationToken);

        if (string.IsNullOrWhiteSpace(request.AnonymousId))
            return Result.Success(await userCart.ToEnrichedDtoAsync(_prices, cancellationToken));

        var guestCart = await _carts.GetByAnonymousIdAsync(request.AnonymousId.Trim(), cancellationToken);
        if (guestCart is null || guestCart.Items.Count == 0)
            return Result.Success(await userCart.ToEnrichedDtoAsync(_prices, cancellationToken));

        if (guestCart.UserId == request.UserId)
            return Result.Success(await guestCart.ToEnrichedDtoAsync(_prices, cancellationToken));

        var existingUserCart = await _carts.GetByUserIdAsync(request.UserId, cancellationToken);
        if (existingUserCart is null || existingUserCart.Items.Count == 0)
        {
            if (existingUserCart is not null && existingUserCart.Id != guestCart.Id)
                _carts.Remove(existingUserCart);

            guestCart.AssignToUser(request.UserId);
            if (await _carts.GetByIdAsync(guestCart.Id, cancellationToken) is null)
                await _carts.AddAsync(guestCart, cancellationToken);
            else
                _carts.Update(guestCart);

            await _uow.SaveChangesAsync(cancellationToken);
            return Result.Success(await guestCart.ToEnrichedDtoAsync(_prices, cancellationToken));
        }

        try
        {
            existingUserCart.MergeFrom(guestCart);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<CartDto>(CartErrors.CartFull);
        }

        _carts.Update(existingUserCart);
        _carts.Remove(guestCart);
        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(await existingUserCart.ToEnrichedDtoAsync(_prices, cancellationToken));
    }

    private async Task<DomainCart> EnsureUserCartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cart = await _carts.GetByUserIdAsync(userId, cancellationToken);
        if (cart is not null) return cart;

        cart = DomainCart.CreateForUser(userId);
        await _carts.AddAsync(cart, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);
        return cart;
    }
}
