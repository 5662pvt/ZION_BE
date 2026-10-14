using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetReviewSummary;

public class GetReviewSummaryQueryHandler : IRequestHandler<GetReviewSummaryQuery, Result<ReviewSummaryDto>>
{
    private readonly IProductRatingSummaryRepository _summaries;

    public GetReviewSummaryQueryHandler(IProductRatingSummaryRepository summaries) => _summaries = summaries;

    public async Task<Result<ReviewSummaryDto>> Handle(GetReviewSummaryQuery request, CancellationToken cancellationToken)
    {
        var summary = await _summaries.GetByIdAsync(request.ProductId, cancellationToken);
        return Result.Success(summary.ToSummaryDto(request.ProductId));
    }
}
