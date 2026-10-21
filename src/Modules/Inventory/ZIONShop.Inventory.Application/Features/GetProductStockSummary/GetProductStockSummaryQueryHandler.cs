using MediatR;
using ZIONShop.Contracts.Inventory;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetProductStockSummary;

public class GetProductStockSummaryQueryHandler : IRequestHandler<GetProductStockSummaryQuery, Result<IReadOnlyList<ProductAvailabilityDto>>>
{
    private readonly IProductStockSummaryReader _reader;

    public GetProductStockSummaryQueryHandler(IProductStockSummaryReader reader) => _reader = reader;

    public async Task<Result<IReadOnlyList<ProductAvailabilityDto>>> Handle(GetProductStockSummaryQuery request, CancellationToken cancellationToken)
    {
        var ids = request.ProductIds.Distinct().ToList();
        if (ids.Count == 0)
            return Result.Success<IReadOnlyList<ProductAvailabilityDto>>(Array.Empty<ProductAvailabilityDto>());

        var byProduct = await _reader.GetAvailableByProductAsync(ids, cancellationToken);

        var result = ids
            .Select(id =>
            {
                var available = byProduct.TryGetValue(id, out var value) ? value : 0;
                return new ProductAvailabilityDto(id, available, available > 0);
            })
            .ToList();

        return Result.Success<IReadOnlyList<ProductAvailabilityDto>>(result);
    }
}
