using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.Inventory.Application.Interfaces;
using ZIONShop.Inventory.Application.Mappings;
using ZIONShop.Inventory.Domain.Entities;
using ZIONShop.Inventory.Domain.Enums;
using ZIONShop.Inventory.Domain.Exceptions;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.AdjustStock;

public class AdjustStockCommandHandler : IRequestHandler<AdjustStockCommand, Result<InventoryItemDto>>
{
    private readonly IInventoryItemRepository _items;
    private readonly IStockMovementRepository _movements;
    private readonly IInventoryUnitOfWork _uow;
    private readonly IProductCatalogReader _catalog;
    private readonly TimeProvider _timeProvider;

    public AdjustStockCommandHandler(
        IInventoryItemRepository items,
        IStockMovementRepository movements,
        IInventoryUnitOfWork uow,
        IProductCatalogReader catalog,
        TimeProvider timeProvider)
    {
        _items = items;
        _movements = movements;
        _uow = uow;
        _catalog = catalog;
        _timeProvider = timeProvider;
    }

    public async Task<Result<InventoryItemDto>> Handle(AdjustStockCommand request, CancellationToken cancellationToken)
    {
        var item = await _items.GetByProductIdAsync(request.ProductId, cancellationToken);
        if (item is null)
        {
            var product = await _catalog.GetAsync(request.ProductId, cancellationToken);
            if (product is null) return Result.Failure<InventoryItemDto>(InventoryErrors.ProductNotFound);

            item = InventoryItem.Create(product.ProductId, product.Sku, product.Name, 0);
            await _items.AddAsync(item, cancellationToken);
        }

        var result = item.Adjust(request.Delta, request.Reason);
        if (result.IsFailure) return Result.Failure<InventoryItemDto>(result.Error);

        await _movements.AddAsync(
            StockMovement.Create(item.ProductId, StockMovementType.Adjust, request.Delta, request.Reason, _timeProvider.GetUtcNow().UtcDateTime, request.CreatedBy),
            cancellationToken);

        try
        {
            await _uow.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure<InventoryItemDto>(InventoryErrors.ConcurrencyConflict);
        }

        return Result.Success(item.ToDto());
    }
}
