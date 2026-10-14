using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.HideReview;

public record HideReviewCommand(Guid ReviewId, string? Reason) : IRequest<Result<AdminReviewDto>>;
