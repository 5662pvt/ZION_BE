using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Exceptions;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.ActivateFlashSale;

public class ActivateFlashSaleCommandHandler : IRequestHandler<ActivateFlashSaleCommand, Result<FlashSaleDto>>
{
    private readonly IFlashSaleRepository _flashSales;
    private readonly IFlashSaleItemRepository _items;
    private readonly IPromotionsUnitOfWork _uow;

    public ActivateFlashSaleCommandHandler(IFlashSaleRepository flashSales, IFlashSaleItemRepository items, IPromotionsUnitOfWork uow)
    {
        _flashSales = flashSales;
        _items = items;
        _uow = uow;
    }

    public async Task<Result<FlashSaleDto>> Handle(ActivateFlashSaleCommand request, CancellationToken cancellationToken)
    {
        var flashSale = await _flashSales.GetByIdAsync(request.FlashSaleId, cancellationToken);
        if (flashSale is null) return Result.Failure<FlashSaleDto>(PromotionsErrors.FlashSaleNotFound);

        var result = flashSale.Activate();
        if (result.IsFailure) return Result.Failure<FlashSaleDto>(result.Error);

        _flashSales.Update(flashSale);
        await _uow.SaveChangesAsync(cancellationToken);

        var items = await _items.GetByFlashSaleIdAsync(flashSale.Id, cancellationToken);
        return Result.Success(flashSale.ToDto(items));
    }
}
