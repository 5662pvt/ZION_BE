using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.Reviews.Application.Interfaces;
using ZIONShop.Reviews.Application.Mappings;
using ZIONShop.Reviews.Domain.Exceptions;
using ZIONShop.Reviews.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.RemoveReviewReply;

public class RemoveReviewReplyCommandHandler : IRequestHandler<RemoveReviewReplyCommand, Result<AdminReviewDto>>
{
    private readonly IReviewRepository _reviews;
    private readonly IReviewsUnitOfWork _uow;

    public RemoveReviewReplyCommandHandler(IReviewRepository reviews, IReviewsUnitOfWork uow)
    {
        _reviews = reviews;
        _uow = uow;
    }

    public async Task<Result<AdminReviewDto>> Handle(RemoveReviewReplyCommand request, CancellationToken cancellationToken)
    {
        var review = await _reviews.GetByIdAsync(request.ReviewId, cancellationToken);
        if (review is null) return Result.Failure<AdminReviewDto>(ReviewsErrors.ReviewNotFound);

        var result = review.RemoveReply();
        if (result.IsFailure) return Result.Failure<AdminReviewDto>(result.Error);

        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(review.ToAdminDto());
    }
}
