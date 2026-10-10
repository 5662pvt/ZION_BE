using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZIONShop.Promotions.Application.Services;

namespace ZIONShop.Promotions.Infrastructure.BackgroundServices;

public class StaleReservationReleaseBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StaleReservationReleaseBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public StaleReservationReleaseBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<StaleReservationReleaseBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("Promotions:StaleReservationScanSeconds", 300)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<StaleReservationReleaseProcessor>();
                await processor.ReleaseStaleReservationsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Stale coupon reservation release scan failed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Shutting down.
            }
        }
    }
}
