using MediatR;
using ZIONShop.Cart.Application.DTOs;
using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Application.Mappings;
using ZIONShop.Cart.Domain.Repositories;
using ZIONShop.SharedKernel.Results;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Application.Features.GetCart;

public class GetCartQueryHandler : IRequestHandler<GetCartQuery, Result<CartDto>>
{
    private readonly ICartRepository _carts;
    private readonly ICartUnitOfWork _uow;

    public GetCartQueryHandler(ICartRepository carts, ICartUnitOfWork uow)
    {
        _carts = carts;
        _uow = uow;
    }

    public async Task<Result<CartDto>> Handle(GetCartQuery request, CancellationToken cancellationToken)
    {
        DomainCart? cart = null;
        if (request.UserId.HasValue)
        {
            cart = await _carts.GetByUserIdAsync(request.UserId.Value, cancellationToken);
            if (cart is null)
            {
                cart = DomainCart.CreateForUser(request.UserId.Value);
                await _carts.AddAsync(cart, cancellationToken);
                await _uow.SaveChangesAsync(cancellationToken);
            }
        }
        else if (!string.IsNullOrWhiteSpace(request.AnonymousId))
        {
            cart = await _carts.GetByAnonymousIdAsync(request.AnonymousId, cancellationToken);
            if (cart is null)
            {
                cart = DomainCart.CreateForGuest(request.AnonymousId);
                await _carts.AddAsync(cart, cancellationToken);
                await _uow.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            cart = DomainCart.CreateForGuest(Guid.NewGuid().ToString("N"));
            await _carts.AddAsync(cart, cancellationToken);
            await _uow.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(cart.ToDto());
    }
}
