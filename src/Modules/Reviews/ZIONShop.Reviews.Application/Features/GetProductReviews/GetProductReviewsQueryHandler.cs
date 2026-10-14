using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Enums;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetProductReviews;

public class GetProductReviewsQueryHandler : IRequestHandler<GetProductReviewsQuery, Result<PagedResult<ReviewDto>>>
{
    private readonly IReviewRepository _reviews;

    public GetProductReviewsQueryHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task<Result<PagedResult<ReviewDto>>> Handle(GetProductReviewsQuery request, CancellationToken cancellationToken)
    {
        var query = _reviews.Query().AsNoTracking()
            .Where(r => r.ProductId == request.ProductId && r.Status == ReviewStatus.Published);

        if (request.Rating.HasValue)
            query = query.Where(r => r.Rating == request.Rating.Value);

        query = query.OrderByDescending(r => r.CreatedDate);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var dtos = items.Select(r => r.ToDto()).ToList();
        return Result.Success(PagedResult<ReviewDto>.Create(dtos, page, pageSize, total));
    }
}
