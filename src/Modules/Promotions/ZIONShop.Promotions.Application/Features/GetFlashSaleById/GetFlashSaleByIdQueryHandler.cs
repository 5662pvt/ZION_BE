using MediatR;
using ZIONShop.Contracts.Products;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.GetFlashSaleById;

public class GetFlashSaleByIdQueryHandler : IRequestHandler<GetFlashSaleByIdQuery, Result<FlashSaleDto>>
{
    private readonly IFlashSaleRepository _flashSales;
    private readonly IFlashSaleItemRepository _items;
    private readonly IProductCatalogReader _catalogReader;

    public GetFlashSaleByIdQueryHandler(IFlashSaleRepository flashSales, IFlashSaleItemRepository items, IProductCatalogReader catalogReader)
    {
        _flashSales = flashSales;
        _items = items;
        _catalogReader = catalogReader;
    }

    public async Task<Result<FlashSaleDto>> Handle(GetFlashSaleByIdQuery request, CancellationToken cancellationToken)
    {
        var flashSale = await _flashSales.GetByIdAsync(request.FlashSaleId, cancellationToken);
        if (flashSale is null) return Result.Failure<FlashSaleDto>(PromotionsErrors.FlashSaleNotFound);

        var items = await _items.GetByFlashSaleIdAsync(flashSale.Id, cancellationToken);
        var variants = await _catalogReader.GetVariantsAsync(items.Select(i => i.VariantId).Distinct().ToList(), cancellationToken);
        var variantById = variants.ToDictionary(v => v.VariantId);

        var itemDtos = items.Select(i =>
        {
            variantById.TryGetValue(i.VariantId, out var variant);
            return i.ToDto(variant?.Sku, variant?.VariantLabel);
        }).ToList();

        return Result.Success(flashSale.ToDto(itemDtos));
    }
}
