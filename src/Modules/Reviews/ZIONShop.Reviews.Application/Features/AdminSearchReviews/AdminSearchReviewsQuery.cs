using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Reviews.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Reviews.Application.Features.AdminSearchReviews;

public record AdminSearchReviewsQuery(int Page, int PageSize, string? Status, int? Rating, string? Keyword)
    : IRequest<Result<PagedResult<AdminReviewDto>>>;
