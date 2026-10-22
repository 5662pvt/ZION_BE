using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ZIONShop.Loyalty.Application.Services;

namespace ZIONShop.Loyalty.Infrastructure.BackgroundServices;

public class LoyaltyExpiryBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoyaltyExpiryBackgroundService> _logger;
    private readonly TimeSpan _interval;

    public LoyaltyExpiryBackgroundService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<LoyaltyExpiryBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(Math.Max(1, configuration.GetValue("Loyalty:ExpiryScanMinutes", 60)));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<LoyaltyExpiryProcessor>();
                await processor.ExpireLotsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Loyalty point lot expiry scan failed");
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
