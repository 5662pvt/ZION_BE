using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Users.Application.Features.GetMyWishlistIds;

public record GetMyWishlistIdsQuery(Guid AuthUserId) : IRequest<Result<IReadOnlyList<Guid>>>;
