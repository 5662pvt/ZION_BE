using MediatR;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetAvailability;

public class GetAvailabilityQueryHandler : IRequestHandler<GetAvailabilityQuery, Result<AvailabilityDto>>
{
    private readonly IInventoryItemRepository _items;

    public GetAvailabilityQueryHandler(IInventoryItemRepository items) => _items = items;

    public async Task<Result<AvailabilityDto>> Handle(GetAvailabilityQuery request, CancellationToken cancellationToken)
    {
        var item = await _items.GetByProductIdAsync(request.ProductId, cancellationToken);
        if (item is null)
            return Result.Success(new AvailabilityDto(request.ProductId, 0, false));

        return Result.Success(new AvailabilityDto(item.ProductId, item.Available, item.Available > 0));
    }
}
