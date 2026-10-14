using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetProductReviews;

public record GetProductReviewsQuery(Guid ProductId, int Page, int PageSize, int? Rating) : IRequest<Result<PagedResult<ReviewDto>>>;
