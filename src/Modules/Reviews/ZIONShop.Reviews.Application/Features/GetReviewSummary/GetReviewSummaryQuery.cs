using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetReviewSummary;

public record GetReviewSummaryQuery(Guid ProductId) : IRequest<Result<ReviewSummaryDto>>;
