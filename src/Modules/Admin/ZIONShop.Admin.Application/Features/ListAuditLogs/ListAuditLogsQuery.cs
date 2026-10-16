using MediatR;
using ZIONShop.Admin.Application.DTOs;
using ZIONShop.Common.Pagination;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Admin.Application.Features.ListAuditLogs;

public record ListAuditLogsQuery(
    int Page,
    int PageSize,
    string? Actor,
    string? Action,
    string? ResourceType,
    DateTime? From,
    DateTime? To) : IRequest<Result<PagedResult<AuditLogDto>>>;
