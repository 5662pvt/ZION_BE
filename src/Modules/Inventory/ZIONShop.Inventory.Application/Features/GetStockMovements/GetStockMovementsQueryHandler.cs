using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Common.Pagination;
using ZIONShop.Inventory.Application.DTOs;
using ZIONShop.Inventory.Application.Mappings;
using ZIONShop.Inventory.Domain.Repositories;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Inventory.Application.Features.GetStockMovements;

public class GetStockMovementsQueryHandler : IRequestHandler<GetStockMovementsQuery, Result<PagedResult<StockMovementDto>>>
{
    private readonly IStockMovementRepository _movements;

    public GetStockMovementsQueryHandler(IStockMovementRepository movements) => _movements = movements;

    public async Task<Result<PagedResult<StockMovementDto>>> Handle(GetStockMovementsQuery request, CancellationToken cancellationToken)
    {
        var query = _movements.Query().AsNoTracking()
            .Where(m => m.ProductId == request.ProductId)
            .OrderByDescending(m => m.CreatedAt);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        var dtos = items.Select(m => m.ToDto()).ToList();
        return Result.Success(PagedResult<StockMovementDto>.Create(dtos, page, pageSize, total));
    }
}
