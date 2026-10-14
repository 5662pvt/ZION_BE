using MediatR;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetReviewSummaries;

public record GetReviewSummariesQuery(IReadOnlyList<Guid> ProductIds) : IRequest<Result<IReadOnlyList<ProductRatingDto>>>;
