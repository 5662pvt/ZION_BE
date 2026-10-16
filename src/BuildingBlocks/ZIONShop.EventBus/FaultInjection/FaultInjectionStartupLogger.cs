using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZIONShop.EventBus.FaultInjection;

/// <summary>Warns once at startup when <c>FaultInjection:CrashBeforeAckEventType</c> is set outside
/// Development, where <see cref="CrashBeforeAckFaultInjector"/> ignores it.</summary>
public sealed class FaultInjectionStartupLogger : IHostedService
{
    private readonly IHostEnvironment _environment;
    private readonly FaultInjectionOptions _options;
    private readonly ILogger<FaultInjectionStartupLogger> _logger;

    public FaultInjectionStartupLogger(IHostEnvironment environment, IOptions<FaultInjectionOptions> options, ILogger<FaultInjectionStartupLogger> logger)
    {
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_environment.IsDevelopment() && !string.IsNullOrEmpty(_options.CrashBeforeAckEventType))
        {
            _logger.LogWarning(
                "FaultInjection:CrashBeforeAckEventType is set to '{EventType}' but environment is '{Environment}' (not Development); ignoring",
                _options.CrashBeforeAckEventType, _environment.EnvironmentName);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
