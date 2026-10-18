using MediatR;
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

    public GetFlashSaleByIdQueryHandler(IFlashSaleRepository flashSales, IFlashSaleItemRepository items)
    {
        _flashSales = flashSales;
        _items = items;
    }

    public async Task<Result<FlashSaleDto>> Handle(GetFlashSaleByIdQuery request, CancellationToken cancellationToken)
    {
        var flashSale = await _flashSales.GetByIdAsync(request.FlashSaleId, cancellationToken);
        if (flashSale is null) return Result.Failure<FlashSaleDto>(PromotionsErrors.FlashSaleNotFound);

        var items = await _items.GetByFlashSaleIdAsync(flashSale.Id, cancellationToken);
        return Result.Success(flashSale.ToDto(items));
    }
}
