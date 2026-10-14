using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetReviewSummaries;

public class GetReviewSummariesQueryHandler : IRequestHandler<GetReviewSummariesQuery, Result<IReadOnlyList<ProductRatingDto>>>
{
    private readonly IProductRatingSummaryRepository _summaries;

    public GetReviewSummariesQueryHandler(IProductRatingSummaryRepository summaries) => _summaries = summaries;

    public async Task<Result<IReadOnlyList<ProductRatingDto>>> Handle(GetReviewSummariesQuery request, CancellationToken cancellationToken)
    {
        var distinctIds = request.ProductIds.Distinct().ToList();
        var found = await _summaries.Query()
            .Where(s => distinctIds.Contains(s.ProductId))
            .ToDictionaryAsync(s => s.ProductId, cancellationToken);

        var results = distinctIds
            .Select(id => (found.TryGetValue(id, out var summary) ? summary : null).ToRatingDto(id))
            .ToList();

        return Result.Success<IReadOnlyList<ProductRatingDto>>(results);
    }
}
