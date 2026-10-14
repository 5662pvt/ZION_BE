using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.ShowReview;

public record ShowReviewCommand(Guid ReviewId) : IRequest<Result<AdminReviewDto>>;
