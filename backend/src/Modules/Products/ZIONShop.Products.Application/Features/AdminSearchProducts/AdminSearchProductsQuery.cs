using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Products.Application.DTOs;
using ZIONShop.Products.Domain.Enums;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Application.Features.AdminSearchProducts;

public record AdminSearchProductsQuery(
    int Page,
    int PageSize,
    string? Keyword,
    Guid? CategoryId,
    ProductStatus? Status) : IRequest<Result<PagedResult<ProductSummaryDto>>>;
