using MediatR;
using ZIONShop.Common.Pagination;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.SearchStock;

public record SearchStockQuery(int Page, int PageSize, string? Keyword, bool LowStockOnly) : IRequest<Result<PagedResult<InventoryItemDto>>>;
