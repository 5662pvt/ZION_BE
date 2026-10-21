using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetAvailabilityBatch;

public class GetAvailabilityBatchQueryHandler : IRequestHandler<GetAvailabilityBatchQuery, Result<IReadOnlyList<AvailabilityDto>>>
{
    private readonly IInventoryItemRepository _items;

    public GetAvailabilityBatchQueryHandler(IInventoryItemRepository items) => _items = items;

    public async Task<Result<IReadOnlyList<AvailabilityDto>>> Handle(GetAvailabilityBatchQuery request, CancellationToken cancellationToken)
    {
        var ids = request.ProductIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result.Success<IReadOnlyList<AvailabilityDto>>(Array.Empty<AvailabilityDto>());

        var items = await _items.Query()
            .AsNoTracking()
            .Where(i => ids.Contains(i.VariantId))
            .ToListAsync(cancellationToken);

        var byVariantId = items.ToDictionary(i => i.VariantId);

        var result = ids
            .Select(id => byVariantId.TryGetValue(id, out var item)
                ? new AvailabilityDto(item.ProductId, item.VariantId, item.VariantLabel, item.Available, item.Available > 0)
                : new AvailabilityDto(id, id, null, 0, false))
            .ToList();

        return Result.Success<IReadOnlyList<AvailabilityDto>>(result);
    }
}
