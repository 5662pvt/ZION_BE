using MediatR;
using ZIONShop.Reviews.Application.Services;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.DeleteReview;

public class DeleteReviewCommandHandler : IRequestHandler<DeleteReviewCommand, Result>
{
    private readonly IReviewRepository _reviews;
    private readonly ProductRatingSummaryUpdater _summaryUpdater;

    public DeleteReviewCommandHandler(IReviewRepository reviews, ProductRatingSummaryUpdater summaryUpdater)
    {
        _reviews = reviews;
        _summaryUpdater = summaryUpdater;
    }

    public async Task<Result> Handle(DeleteReviewCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviews.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null || review.UserId != request.UserId)
            return Result.Failure(ReviewsErrors.ReviewNotFound);

        _reviews.Remove(review);
        await _summaryUpdater.ApplyAsync(review.ProductId, review.Id, includedRating: null, cancellationToken);

        return Result.Success();
    }
}
