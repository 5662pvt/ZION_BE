using MediatR;
using ZIONShop.Contracts.Products;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.UpdateFlashSale;

public class UpdateFlashSaleCommandHandler : IRequestHandler<UpdateFlashSaleCommand, Result<FlashSaleDto>>
{
    private readonly IFlashSaleRepository _flashSales;
    private readonly IFlashSaleItemRepository _items;
    private readonly IProductCatalogReader _catalogReader;
    private readonly IPromotionsUnitOfWork _uow;

    public UpdateFlashSaleCommandHandler(
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

    public async Task<Result<FlashSaleDto>> Handle(UpdateFlashSaleCommand request, CancellationToken cancellationToken)
    {
        var flashSale = await _flashSales.GetByIdAsync(request.FlashSaleId, cancellationToken);
        if (flashSale is null) return Result.Failure<FlashSaleDto>(PromotionsErrors.FlashSaleNotFound);

        flashSale.Update(request.Name, request.StartsAt, request.EndsAt);
        _flashSales.Update(flashSale);
        await _uow.SaveChangesAsync(cancellationToken);

        var items = await _items.GetByFlashSaleIdAsync(flashSale.Id, cancellationToken);
        var variants = await _catalogReader.ListVariantsAsync(cancellationToken);
        var variantById = variants.ToDictionary(v => v.VariantId);

        var itemDtos = items.Select(i =>
        {
            variantById.TryGetValue(i.VariantId, out var variant);
            return i.ToDto(variant?.Sku, variant?.VariantLabel);
        }).ToList();

        return Result.Success(flashSale.ToDto(itemDtos));
    }
}
