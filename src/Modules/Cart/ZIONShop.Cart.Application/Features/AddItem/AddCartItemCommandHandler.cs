using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Application.Mappings;
using ZIONShop.Cart.Domain.Exceptions;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.SharedKernel.Results;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Application.Features.AddItem;

public class AddCartItemCommandHandler : IRequestHandler<AddCartItemCommand, Result<CartDto>>
{
    private readonly ICartRepository _carts;
    private readonly ICartUnitOfWork _uow;
    private readonly IProductPriceLookup _prices;

    public AddCartItemCommandHandler(ICartRepository carts, ICartUnitOfWork uow, IProductPriceLookup prices)
    {
        _carts = carts;
        _uow = uow;
        _prices = prices;
    }

    public async Task<Result<CartDto>> Handle(AddCartItemCommand request, CancellationToken cancellationToken)
    {
        var price = await _prices.GetAsync(request.ProductId, cancellationToken);
        if (price is null || !price.IsAvailable)
            return Result.Failure<CartDto>(CartErrors.ProductUnavailable);

        DomainCart? cart;
        if (request.UserId.HasValue)
        {
            cart = await _carts.GetByUserIdAsync(request.UserId.Value, cancellationToken)
                   ?? DomainCart.CreateForUser(request.UserId.Value);
        }
        else
        {
            cart = await _carts.GetByAnonymousIdAsync(request.AnonymousId!, cancellationToken)
                   ?? DomainCart.CreateForGuest(request.AnonymousId!);
        }

        var isNew = cart.CreatedDate == default || cart.Items.Count == 0 && cart.CreatedDate.AddSeconds(1) > DateTime.UtcNow;
        try
        {
            cart.AddItem(request.ProductId, price.Name, request.Quantity, price.UnitPrice, price.Currency);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<CartDto>(CartErrors.CartFull);
        }
        catch (ArgumentException)
        {
            return Result.Failure<CartDto>(CartErrors.InvalidQuantity);
        }

        if (isNew && await _carts.GetByIdAsync(cart.Id, cancellationToken) is null)
            await _carts.AddAsync(cart, cancellationToken);
        else
            _carts.Update(cart);

        await _uow.SaveChangesAsync(cancellationToken);
        return Result.Success(cart.ToDto());
    }
}
