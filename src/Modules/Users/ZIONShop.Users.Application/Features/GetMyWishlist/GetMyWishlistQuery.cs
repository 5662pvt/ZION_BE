using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.SharedKernel.Results;
using ZIONShop.Users.Application.DTOs;

namespace ZIONShop.Users.Application.Features.GetMyWishlist;

public record GetMyWishlistQuery(Guid AuthUserId, int Page, int PageSize) : IRequest<Result<PagedResult<WishlistItemDto>>>;
