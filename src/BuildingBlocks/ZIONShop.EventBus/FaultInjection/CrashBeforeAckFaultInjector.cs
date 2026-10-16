using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ZIONShop.EventBus.FaultInjection;

public sealed class CrashBeforeAckFaultInjector : IFaultInjector
{
    private readonly IHostEnvironment _environment;
    private readonly FaultInjectionOptions _options;
    private readonly IProcessTerminator _terminator;
    private readonly ILogger<CrashBeforeAckFaultInjector> _logger;

    public CrashBeforeAckFaultInjector(
        IHostEnvironment environment,
        IOptions<FaultInjectionOptions> options,
        IProcessTerminator terminator,
        ILogger<CrashBeforeAckFaultInjector> logger)
    {
        _environment = environment;
        _options = options.Value;
        _terminator = terminator;
        _logger = logger;
    }

    public bool ShouldCrashBeforeAck(string eventType)
    {
        if (!_environment.IsDevelopment()) return false;

        var configured = _options.CrashBeforeAckEventType;
        return !string.IsNullOrEmpty(configured) && string.Equals(configured, eventType, StringComparison.OrdinalIgnoreCase);
    }

    public void Crash(string eventType, string? messageId)
    {
        _logger.LogCritical("FAULT INJECTION: crashing before ACK for {EventType} {MessageId}", eventType, messageId);
        _terminator.Terminate(70);
    }
}
