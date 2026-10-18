using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.SearchFlashSales;

public record SearchFlashSalesQuery(int Page, int PageSize, string? Keyword) : IRequest<Result<PagedResult<FlashSaleSummaryDto>>>;
