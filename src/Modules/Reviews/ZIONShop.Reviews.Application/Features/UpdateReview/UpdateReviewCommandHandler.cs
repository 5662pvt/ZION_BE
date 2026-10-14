using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Application.Services;
using ZIONShop.Reviews.Domain.Enums;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.UpdateReview;

public class UpdateReviewCommandHandler : IRequestHandler<UpdateReviewCommand, Result<ReviewDto>>
{
    private readonly IReviewRepository _reviews;
    private readonly ProductRatingSummaryUpdater _summaryUpdater;

    public UpdateReviewCommandHandler(IReviewRepository reviews, ProductRatingSummaryUpdater summaryUpdater)
    {
        _reviews = reviews;
        _summaryUpdater = summaryUpdater;
    }

    public async Task<Result<ReviewDto>> Handle(UpdateReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviews.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null || review.UserId != request.UserId)
            return Result.Failure<ReviewDto>(ReviewsErrors.ReviewNotFound);

        review.UpdateContent(request.Rating, request.Title, request.Content.Trim());

        var includedRating = review.Status == ReviewStatus.Published ? review.Rating : (int?)null;
        await _summaryUpdater.ApplyAsync(review.ProductId, review.Id, includedRating, cancellationToken);

        return Result.Success(review.ToDto());
    }
}
