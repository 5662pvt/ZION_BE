using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.CreateReview;

public record CreateReviewCommand(Guid UserId, Guid ProductId, int Rating, string? Title, string Content) : IRequest<Result<ReviewDto>>;
