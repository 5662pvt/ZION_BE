using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Application.Services;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.HideReview;

public class HideReviewCommandHandler : IRequestHandler<HideReviewCommand, Result<AdminReviewDto>>
{
    private readonly IReviewRepository _reviews;
    private readonly ProductRatingSummaryUpdater _summaryUpdater;

    public HideReviewCommandHandler(IReviewRepository reviews, ProductRatingSummaryUpdater summaryUpdater)
    {
        _reviews = reviews;
        _summaryUpdater = summaryUpdater;
    }

    public async Task<Result<AdminReviewDto>> Handle(HideReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviews.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null) return Result.Failure<AdminReviewDto>(ReviewsErrors.ReviewNotFound);

        var result = review.Hide(request.Reason);
        if (result.IsFailure) return Result.Failure<AdminReviewDto>(result.Error);

        await _summaryUpdater.ApplyAsync(review.ProductId, review.Id, includedRating: null, cancellationToken);

        return Result.Success(review.ToAdminDto());
    }
}
