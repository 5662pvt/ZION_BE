using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.GetMyReviews;

public record GetMyReviewsQuery(Guid UserId, int Page, int PageSize) : IRequest<Result<PagedResult<MyReviewDto>>>;
