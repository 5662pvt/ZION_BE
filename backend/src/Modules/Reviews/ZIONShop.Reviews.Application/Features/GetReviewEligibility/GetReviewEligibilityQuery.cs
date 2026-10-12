using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetReviewEligibility;

public record GetReviewEligibilityQuery(Guid UserId, Guid ProductId) : IRequest<Result<ReviewEligibilityDto>>;
