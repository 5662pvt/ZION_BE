using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Domain.Repositories;

namespace ZIONShop.Users.Application.Features.GetMyWishlistIds;

public class GetMyWishlistIdsQueryHandler : IRequestHandler<GetMyWishlistIdsQuery, Result<IReadOnlyList<Guid>>>
{
    private readonly IWishlistRepository _wishlist;

    public GetMyWishlistIdsQueryHandler(IWishlistRepository wishlist) => _wishlist = wishlist;

    public async Task<Result<IReadOnlyList<Guid>>> Handle(GetMyWishlistIdsQuery request, CancellationToken cancellationToken)
    {
        IReadOnlyList<Guid> ids = await _wishlist.Query()
            .Where(w => w.UserId == request.AuthUserId)
            .Select(w => w.ProductId)
            .ToListAsync(cancellationToken);

        return Result.Success(ids);
    }
}
