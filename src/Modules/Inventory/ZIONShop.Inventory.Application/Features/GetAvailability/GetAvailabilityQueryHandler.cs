using MediatR;
using ZIONShop.Contracts.Products;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.Inventory.Application.Services;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetAvailability;

public class GetAvailabilityQueryHandler : IRequestHandler<GetAvailabilityQuery, Result<AvailabilityDto>>
{
    private readonly IInventoryItemRepository _items;
    private readonly IProductCatalogReader _catalog;

    public GetAvailabilityQueryHandler(IInventoryItemRepository items, IProductCatalogReader catalog)
    {
        _items = items;
        _catalog = catalog;
    }

    public async Task<Result<AvailabilityDto>> Handle(GetAvailabilityQuery request, CancellationToken cancellationToken)
    {
        Guid variantId;
        if (request.IsVariantId)
        {
            variantId = request.Id;
        }
        else
        {
            var resolved = await ProductOrVariantIdResolver.ResolveAsync(_catalog, request.Id, cancellationToken);
            if (resolved.IsFailure) return Result.Failure<AvailabilityDto>(resolved.Error);
            variantId = resolved.Value;
        }

        var item = await _items.GetByVariantIdAsync(variantId, cancellationToken);
        if (item is null)
            return Result.Success(new AvailabilityDto(request.Id, variantId, null, 0, false));

        return Result.Success(new AvailabilityDto(item.ProductId, item.VariantId, item.VariantLabel, item.Available, item.Available > 0));
    }
}
