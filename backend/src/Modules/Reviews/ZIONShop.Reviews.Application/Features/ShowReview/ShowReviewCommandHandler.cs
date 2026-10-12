using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Application.Services;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.ShowReview;

public class ShowReviewCommandHandler : IRequestHandler<ShowReviewCommand, Result<AdminReviewDto>>
{
    private readonly IReviewRepository _reviews;
    private readonly ProductRatingSummaryUpdater _summaryUpdater;

    public ShowReviewCommandHandler(IReviewRepository reviews, ProductRatingSummaryUpdater summaryUpdater)
    {
        _reviews = reviews;
        _summaryUpdater = summaryUpdater;
    }

    public async Task<Result<AdminReviewDto>> Handle(ShowReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviews.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null) return Result.Failure<AdminReviewDto>(ReviewsErrors.ReviewNotFound);

        var result = review.Show();
        if (result.IsFailure) return Result.Failure<AdminReviewDto>(result.Error);

        await _summaryUpdater.ApplyAsync(review.ProductId, review.Id, includedRating: review.Rating, cancellationToken);

        return Result.Success(review.ToAdminDto());
    }
}
