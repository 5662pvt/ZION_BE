namespace ZIONShop.EventBus.FaultInjection;

/// <summary>Seam over process termination so <see cref="CrashBeforeAckFaultInjector"/> is unit-testable
/// without actually killing the test process.</summary>
public interface IProcessTerminator
{
    void Terminate(int exitCode);
}

/// <summary>Uses <see cref="Environment.FailFast(string?)"/> rather than <see cref="Environment.Exit(int)"/>:
/// FailFast terminates immediately without running <c>finally</c> blocks, <c>AppDomain.ProcessExit</c>
/// handlers, or the host's graceful <see cref="Microsoft.Extensions.Hosting.IHostedService.StopAsync"/>
/// shutdown path. Exit would risk letting some shutdown hook flush state or otherwise behave as if the
/// message had been handled cleanly -- the whole point of this hook is to simulate a hard crash where
/// the in-flight message is never ack'd and gets redelivered on restart.</summary>
public sealed class EnvironmentProcessTerminator : IProcessTerminator
{
    public void Terminate(int exitCode) =>
        Environment.FailFast($"Fault injection: terminating process (exit code {exitCode}).");
}
