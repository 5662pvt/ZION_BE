using MediatR;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.AdjustStock;

/// <summary>When <paramref name="IsVariantId"/> is false, <paramref name="Id"/> is resolved via <see cref="Services.ProductOrVariantIdResolver"/> (legacy product-id route); when true it is used directly as a variant id.</summary>
public record AdjustStockCommand(Guid Id, bool IsVariantId, int Delta, string Reason, string? CreatedBy) : IRequest<Result<InventoryItemDto>>;
