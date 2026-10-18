using MediatR;
using ZIONShop.Promotions.Application.DTOs;
using ZIONShop.Promotions.Application.Interfaces;
using ZIONShop.Promotions.Application.Mappings;
using ZIONShop.Promotions.Domain.Entities;
using ZIONShop.Promotions.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Promotions.Application.Features.CreateFlashSale;

public class CreateFlashSaleCommandHandler : IRequestHandler<CreateFlashSaleCommand, Result<FlashSaleDto>>
{
    private readonly IFlashSaleRepository _flashSales;
    private readonly IPromotionsUnitOfWork _uow;

    public CreateFlashSaleCommandHandler(IFlashSaleRepository flashSales, IPromotionsUnitOfWork uow)
    {
        _flashSales = flashSales;
        _uow = uow;
    }

    public async Task<Result<FlashSaleDto>> Handle(CreateFlashSaleCommand request, CancellationToken cancellationToken)
    {
        var flashSale = FlashSale.Create(request.Name, request.StartsAt, request.EndsAt);

        await _flashSales.AddAsync(flashSale, cancellationToken);
        await _uow.SaveChangesAsync(cancellationToken);

        return Result.Success(flashSale.ToDto(Array.Empty<FlashSaleItem>()));
    }
}
