using MediatR;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.AdjustStock;

public record AdjustStockCommand(Guid ProductId, int Delta, string Reason, string? CreatedBy) : IRequest<Result<InventoryItemDto>>;
