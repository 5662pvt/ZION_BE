using MediatR;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.DeleteReview;

public record DeleteReviewCommand(Guid ReviewId, Guid UserId) : IRequest<Result>;
