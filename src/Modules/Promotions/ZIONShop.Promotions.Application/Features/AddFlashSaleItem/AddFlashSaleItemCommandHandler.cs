using MediatR;
using ZIONShop.Contracts.Products;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.AddFlashSaleItem;

public class AddFlashSaleItemCommandHandler : IRequestHandler<AddFlashSaleItemCommand, Result<FlashSaleItemDto>>
{
    private readonly IFlashSaleRepository _flashSales;
    private readonly IFlashSaleItemRepository _items;
    private readonly IProductCatalogReader _catalogReader;
    private readonly IPromotionsUnitOfWork _uow;

    public AddFlashSaleItemCommandHandler(
        IFlashSaleRepository flashSales,
        IFlashSaleItemRepository items,
        IProductCatalogReader catalogReader,
        IPromotionsUnitOfWork uow)
    {
        _flashSales = flashSales;
        _items = items;
        _catalogReader = catalogReader;
        _uow = uow;
    }

    public async Task<Result<FlashSaleItemDto>> Handle(AddFlashSaleItemCommand request, CancellationToken cancellationToken)
    {
        var flashSale = await _flashSales.GetByIdAsync(request.FlashSaleId, cancellationToken);
        if (flashSale is null) return Result.Failure<FlashSaleItemDto>(PromotionsErrors.FlashSaleNotFound);

        var product = await _catalogReader.GetAsync(request.ProductId, cancellationToken);
        if (product is null) return Result.Failure<FlashSaleItemDto>(PromotionsErrors.FlashSaleProductNotFound);

        var existingItems = await _items.GetByFlashSaleIdAsync(flashSale.Id, cancellationToken);
        if (existingItems.Any(i => i.ProductId == request.ProductId))
            return Result.Failure<FlashSaleItemDto>(PromotionsErrors.FlashSaleItemProductAlreadyAdded);

        var item = FlashSaleItem.Create(flashSale.Id, request.ProductId, request.FlashPrice, request.AllocatedQuantity);
        await _items.AddAsync(item, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(item.ToDto());
    }
}
