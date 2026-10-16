using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using ZIONShop.Api.Filters;
using ZIONShop.Auth.Authorization;
using ZIONShop.Auth.CurrentUser;
using ZIONShop.HealthChecks;
using ZIONShop.Common.Api;
using ZIONShop.Common.Pagination;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.EventBus.RabbitMq;
using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Api.Controllers;

[ApiController]
[Route("api/v1/admin/system")]
[Authorize(Policy = Policies.AdminOnly)]
public class AdminSystemController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;
    private readonly IReadOnlyList<IOutboxAdminStore> _outboxStores;
    private readonly IReadOnlyList<IInboxAdminStore> _inboxStores;
    private readonly IDeadLetterAdmin _deadLetterAdmin;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AdminSystemController> _logger;

    public AdminSystemController(
        HealthCheckService healthCheckService,
        IEnumerable<IOutboxAdminStore> outboxStores,
        IEnumerable<IInboxAdminStore> inboxStores,
        IDeadLetterAdmin deadLetterAdmin,
        ICurrentUserService currentUser,
        ILogger<AdminSystemController> logger)
    {
        _healthCheckService = healthCheckService;
        _outboxStores = outboxStores.ToList();
        _inboxStores = inboxStores.ToList();
        _deadLetterAdmin = deadLetterAdmin;
        _currentUser = currentUser;
        _logger = logger;
    }

    [HttpGet("health")]
    public async Task<IActionResult> Health(CancellationToken ct)
    {
        // No tag predicate here (unlike /health/ready): this admin view must still surface "worker",
        // which is deliberately untagged so it never fails readiness probes.
        var report = await _healthCheckService.CheckHealthAsync(ct);
        var dto = new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checkedAt = DateTime.UtcNow,
            checks = report.ToChecks()
        };
        return Ok(ApiResponse<object>.Ok(dto));
    }

    [HttpGet("modules")]
    public IActionResult Modules()
    {
        var dto = new
        {
            outbox = _outboxStores.Select(s => s.Module).Distinct().OrderBy(m => m, StringComparer.Ordinal).ToList(),
            inbox = _inboxStores.Select(s => s.Module).Distinct().OrderBy(m => m, StringComparer.Ordinal).ToList()
        };
        return Ok(ApiResponse<object>.Ok(dto));
    }

    [HttpGet("outbox")]
    public async Task<IActionResult> ListOutbox(
        [FromQuery] string? module, [FromQuery] string? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        IReadOnlyList<IOutboxAdminStore> stores;
        if (module is not null)
        {
            var match = FindOutboxStore(module);
            if (match is null)
                return InvalidFilter($"Unknown outbox module '{module}'.");
            stores = new[] { match };
        }
        else
        {
            stores = _outboxStores;
        }

        OutboxStatus? statusFilter = null;
        if (status is not null)
        {
            if (!Enum.TryParse(status, ignoreCase: true, out OutboxStatus parsed))
                return InvalidFilter($"Unknown outbox status '{status}'.");
            statusFilter = parsed;
        }

        var take = page * pageSize;
        var totalCount = 0;
        var candidates = new List<OutboxMessageSummary>();
        foreach (var store in stores)
        {
            totalCount += await store.CountAsync(statusFilter, ct);
            candidates.AddRange(await store.ListNewestAsync(statusFilter, take, ct));
        }

        var items = candidates
            .OrderByDescending(m => m.OccurredOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result.Success(PagedResult<OutboxMessageSummary>.Create(items, page, pageSize, totalCount)).ToPagedActionResult();
    }

    [HttpGet("outbox/{module}/{id:guid}")]
    public async Task<IActionResult> GetOutboxDetail(string module, Guid id, CancellationToken ct)
    {
        var store = FindOutboxStore(module);
        if (store is null) return OutboxMessageNotFound(id);

        var detail = await store.GetAsync(id, ct);
        if (detail is null) return OutboxMessageNotFound(id);

        return Ok(ApiResponse<OutboxMessageDetail>.Ok(detail));
    }

    [HttpPost("outbox/{module}/{id:guid}/retry")]
    [AdminAudit("System.OutboxRetry", "Outbox")]
    public async Task<IActionResult> RetryOutbox(string module, Guid id, CancellationToken ct)
    {
        var store = FindOutboxStore(module);
        if (store is null) return OutboxMessageNotFound(id);

        var outcome = await store.RetryAsync(id, ct);
        switch (outcome)
        {
            case OutboxRetryOutcome.NotFound:
                return OutboxMessageNotFound(id);
            case OutboxRetryOutcome.NotFailed:
                return Result.Failure(Error.Conflict("System.OutboxMessageNotFailed", $"Outbox message '{id}' is not in a Failed state."))
                    .ToActionResult();
            default:
                _logger.LogInformation("Admin {AdminId} retried outbox message {MessageId} ({Module})", AdminId(), id, module);
                return Ok(ApiResponse<object>.Ok(new { id, status = "Pending" }));
        }
    }

    [HttpPost("outbox/retry-failed")]
    [AdminAudit("System.OutboxRetryFailed", "Outbox", ResourceIdFromResult = true)]
    public async Task<IActionResult> RetryFailedOutbox([FromQuery] string? module, CancellationToken ct)
    {
        IReadOnlyList<IOutboxAdminStore> stores;
        if (module is not null)
        {
            var match = FindOutboxStore(module);
            if (match is null) return InvalidFilter($"Unknown outbox module '{module}'.");
            stores = new[] { match };
        }
        else
        {
            stores = _outboxStores;
        }

        var retried = 0;
        foreach (var store in stores)
            retried += await store.RetryFailedAsync(ct);

        _logger.LogInformation("Admin {AdminId} retried {Count} failed outbox messages (module filter: {Module})", AdminId(), retried, module ?? "all");
        return Ok(ApiResponse<object>.Ok(new { retried }));
    }

    [HttpGet("inbox")]
    public async Task<IActionResult> ListInbox(
        [FromQuery] string? module, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        IReadOnlyList<IInboxAdminStore> stores;
        if (module is not null)
        {
            var match = _inboxStores.FirstOrDefault(s => string.Equals(s.Module, module, StringComparison.OrdinalIgnoreCase));
            if (match is null) return InvalidFilter($"Unknown inbox module '{module}'.");
            stores = new[] { match };
        }
        else
        {
            stores = _inboxStores;
        }

        var take = page * pageSize;
        var totalCount = 0;
        var candidates = new List<InboxMessageSummary>();
        foreach (var store in stores)
        {
            totalCount += await store.CountAsync(ct);
            candidates.AddRange(await store.ListNewestAsync(take, ct));
        }

        var items = candidates
            .OrderByDescending(m => m.ProcessedOn)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return Result.Success(PagedResult<InboxMessageSummary>.Create(items, page, pageSize, totalCount)).ToPagedActionResult();
    }

    [HttpGet("dlq")]
    public async Task<IActionResult> Dlq([FromQuery] int limit = 20, CancellationToken ct = default)
    {
        limit = limit switch { < 1 => 20, > 100 => 100, _ => limit };
        try
        {
            var result = await _deadLetterAdmin.PeekAsync(limit, ct);
            return Ok(ApiResponse<DeadLetterPeekResult>.Ok(result));
        }
        catch (BrokerUnavailableException)
        {
            return BrokerUnavailable();
        }
    }

    [HttpPost("dlq/requeue")]
    [AdminAudit("System.DlqRequeue", "Dlq", ResourceIdFromResult = true)]
    public async Task<IActionResult> RequeueDlq([FromBody] DlqRequeueBody? body, CancellationToken ct)
    {
        try
        {
            var requeued = await _deadLetterAdmin.RequeueAsync(body?.MessageIds, ct);
            _logger.LogInformation("Admin {AdminId} requeued {Count} DLQ messages", AdminId(), requeued);
            return Ok(ApiResponse<object>.Ok(new { requeued }));
        }
        catch (BrokerUnavailableException)
        {
            return BrokerUnavailable();
        }
    }

    [HttpDelete("dlq/{messageId}")]
    [AdminAudit("System.DlqDelete", "Dlq", RouteKey = "messageId")]
    public async Task<IActionResult> DeleteDlqMessage(string messageId, CancellationToken ct)
    {
        try
        {
            var deleted = await _deadLetterAdmin.DeleteAsync(messageId, ct);
            if (deleted == 0)
                return Result.Failure(Error.NotFound("System.DlqMessageNotFound", $"DLQ message '{messageId}' not found.")).ToActionResult();

            _logger.LogInformation("Admin {AdminId} deleted DLQ message {MessageId}", AdminId(), messageId);
            return Ok(ApiResponse<object>.Ok(new { deleted }));
        }
        catch (BrokerUnavailableException)
        {
            return BrokerUnavailable();
        }
    }

    private IOutboxAdminStore? FindOutboxStore(string module) =>
        _outboxStores.FirstOrDefault(s => string.Equals(s.Module, module, StringComparison.OrdinalIgnoreCase));

    private static IActionResult OutboxMessageNotFound(Guid id) =>
        Result.Failure(Error.NotFound("System.OutboxMessageNotFound", $"Outbox message '{id}' not found.")).ToActionResult();

    private static IActionResult InvalidFilter(string message) =>
        Result.Failure(Error.Validation("System.InvalidFilter", message)).ToActionResult();

    private static IActionResult BrokerUnavailable()
    {
        const string message = "RabbitMQ broker is unavailable.";
        var error = new ErrorDetail { Code = "System.BrokerUnavailable", Message = message };
        return new ObjectResult(ApiResponse.Fail(message, new[] { error })) { StatusCode = StatusCodes.Status503ServiceUnavailable };
    }

    private string AdminId() => _currentUser.Email ?? _currentUser.UserId?.ToString() ?? "unknown";
}

public record DlqRequeueBody(string[]? MessageIds);
