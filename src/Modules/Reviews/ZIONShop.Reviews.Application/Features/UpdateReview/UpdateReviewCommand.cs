using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.UpdateReview;

public record UpdateReviewCommand(Guid ReviewId, Guid UserId, int Rating, string? Title, string Content) : IRequest<Result<ReviewDto>>;
