using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.SearchProducts;

public record SearchProductsQuery(
    int Page,
    int PageSize,
    string? Keyword,
    Guid? CategoryId,
    decimal? MinPrice,
    decimal? MaxPrice,
    string? Sort) : IRequest<Result<PagedResult<ProductSummaryDto>>>;
