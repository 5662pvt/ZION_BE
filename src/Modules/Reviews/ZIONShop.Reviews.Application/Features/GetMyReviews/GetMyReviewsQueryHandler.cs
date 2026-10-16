using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Contracts.Products;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetMyReviews;

public class GetMyReviewsQueryHandler : IRequestHandler<GetMyReviewsQuery, Result<PagedResult<MyReviewDto>>>
{
    private readonly IReviewRepository _reviews;
    private readonly IProductPriceLookup _prices;

    public GetMyReviewsQueryHandler(IReviewRepository reviews, IProductPriceLookup prices)
    {
        _reviews = reviews;
        _prices = prices;
    }

    public async Task<Result<PagedResult<MyReviewDto>>> Handle(GetMyReviewsQuery request, CancellationToken cancellationToken)
    {
        var query = _reviews.Query().AsNoTracking()
            .Where(r => r.UserId == request.UserId)
            .OrderByDescending(r => r.CreatedDate);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var productIds = items.Select(r => r.ProductId).Distinct().ToList();
        var priceInfos = await _prices.GetManyAsync(productIds, cancellationToken);

        var dtos = items
            .Select(r => r.ToMyDto(priceInfos.TryGetValue(r.ProductId, out var info) ? info.Slug : null))
            .ToList();

        return Result.Success(PagedResult<MyReviewDto>.Create(dtos, page, pageSize, total));
    }
}
