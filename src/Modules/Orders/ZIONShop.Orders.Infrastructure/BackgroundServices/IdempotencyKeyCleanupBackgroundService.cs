using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZIONShop.Orders.Infrastructure.Persistence;

namespace ZIONShop.Orders.Infrastructure.BackgroundServices;

public class IdempotencyKeyCleanupBackgroundService : BackgroundService
{
    private const int BatchSize = 500;
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<IdempotencyKeyCleanupBackgroundService> _logger;
    private readonly int _retentionDays;

    public IdempotencyKeyCleanupBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<IdempotencyKeyCleanupBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _retentionDays = configuration.GetValue("Orders:IdempotencyKeyRetentionDays", 7);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Idempotency key cleanup failed");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Shutting down.
            }
        }
    }

    /// <summary>Public so tests can trigger a cleanup pass directly instead of waiting on
    /// <see cref="Interval"/>.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var cutoff = DateTime.UtcNow.AddDays(-_retentionDays);

        var totalDeleted = 0;
        int deletedThisBatch;
        do
        {
            deletedThisBatch = await db.OrderIdempotencyKeys
                .Where(k => k.CreatedAt < cutoff)
                .OrderBy(k => k.CreatedAt)
                .Take(BatchSize)
                .ExecuteDeleteAsync(cancellationToken);
            totalDeleted += deletedThisBatch;
        } while (deletedThisBatch == BatchSize && !cancellationToken.IsCancellationRequested);

        if (totalDeleted > 0)
            _logger.LogInformation("Deleted {Count} expired idempotency keys older than {Cutoff:O}", totalDeleted, cutoff);

        return totalDeleted;
    }
}
