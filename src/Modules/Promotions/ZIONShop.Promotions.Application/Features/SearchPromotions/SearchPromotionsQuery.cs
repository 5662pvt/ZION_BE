using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.SearchPromotions;

public record SearchPromotionsQuery(int Page, int PageSize, string? Keyword, string? Status)
    : IRequest<Result<PagedResult<PromotionSummaryDto>>>;
