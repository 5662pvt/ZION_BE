using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.ReplyToReview;

public record ReplyToReviewCommand(Guid ReviewId, string Content) : IRequest<Result<AdminReviewDto>>;
