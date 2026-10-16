using MediatR;
using Microsoft.EntityFrameworkCore;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.Admin.Domain.Entities;
using ZIONShop.Admin.Domain.Repositories;
using ZIONShop.Common.Pagination;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.ListAuditLogs;

public class ListAuditLogsQueryHandler : IRequestHandler<ListAuditLogsQuery, Result<PagedResult<AuditLogDto>>>
{
    private readonly IAuditLogRepository _auditLogs;

    public ListAuditLogsQueryHandler(IAuditLogRepository auditLogs) => _auditLogs = auditLogs;

    public async Task<Result<PagedResult<AuditLogDto>>> Handle(ListAuditLogsQuery request, CancellationToken cancellationToken)
    {
        IQueryable<AuditLog> query = _auditLogs.Query().AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Actor))
        {
            if (Guid.TryParse(request.Actor, out var actorId))
            {
                query = query.Where(a => a.ActorId == actorId);
            }
            else
            {
                var kw = request.Actor.Trim().ToUpperInvariant();
                query = query.Where(a => a.ActorEmail != null && a.ActorEmail.ToUpper().Contains(kw));
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
            query = query.Where(a => a.Action == request.Action);

        if (!string.IsNullOrWhiteSpace(request.ResourceType))
            query = query.Where(a => a.ResourceType == request.ResourceType);

        if (request.From.HasValue)
            query = query.Where(a => a.OccurredAt >= request.From.Value.Date);

        if (request.To.HasValue)
            query = query.Where(a => a.OccurredAt < request.To.Value.Date.AddDays(1));

        query = query.OrderByDescending(a => a.OccurredAt);

        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 100 ? 20 : request.PageSize;

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogDto(
                a.Id, a.OccurredAt, a.ActorId, a.ActorEmail, a.Action, a.ResourceType, a.ResourceId,
                a.HttpMethod, a.Path, a.StatusCode, a.IpAddress, a.CorrelationId, a.TraceId))
            .ToListAsync(cancellationToken);

        return Result.Success(PagedResult<AuditLogDto>.Create(items, page, pageSize, total));
    }
}
