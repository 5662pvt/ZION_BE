using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Enums;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.AdminSearchReviews;

public class AdminSearchReviewsQueryHandler : IRequestHandler<AdminSearchReviewsQuery, Result<PagedResult<AdminReviewDto>>>
{
    private readonly IReviewRepository _reviews;

    public AdminSearchReviewsQueryHandler(IReviewRepository reviews) => _reviews = reviews;

    public async Task<Result<PagedResult<AdminReviewDto>>> Handle(AdminSearchReviewsQuery request, CancellationToken cancellationToken)
    {
        var query = _reviews.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Status) && Enum.TryParse<ReviewStatus>(request.Status, ignoreCase: true, out var status))
            query = query.Where(r => r.Status == status);

        if (request.Rating.HasValue)
            query = query.Where(r => r.Rating == request.Rating.Value);

        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var kw = EscapeLikePattern(request.Keyword.Trim()).ToUpperInvariant();
            query = query.Where(r =>
                EF.Functions.Like(r.ProductName.ToUpper(), $"%{kw}%") ||
                EF.Functions.Like(r.AuthorName.ToUpper(), $"%{kw}%") ||
                EF.Functions.Like(r.Content.ToUpper(), $"%{kw}%"));
        }

        query = query.OrderByDescending(r => r.CreatedDate);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var dtos = items.Select(r => r.ToAdminDto()).ToList();
        return Result.Success(PagedResult<AdminReviewDto>.Create(dtos, page, pageSize, total));
    }

    private static string EscapeLikePattern(string value) =>
        value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
