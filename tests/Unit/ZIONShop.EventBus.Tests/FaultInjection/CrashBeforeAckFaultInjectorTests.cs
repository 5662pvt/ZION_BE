using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ZIONShop.EventBus.FaultInjection;

namespace ZIONShop.EventBus.Tests.FaultInjection;

public class CrashBeforeAckFaultInjectorTests
{
    [Fact]
    public void ShouldCrashBeforeAck_Should_ReturnTrue_When_DevelopmentAndEventTypeMatchesCaseInsensitively()
    {
        var injector = BuildInjector(Environments.Development, "OrderCreatedIntegrationEvent");

        injector.ShouldCrashBeforeAck("ordercreatedintegrationevent").Should().BeTrue();
    }

    [Fact]
    public void ShouldCrashBeforeAck_Should_ReturnFalse_When_EnvironmentIsNotDevelopment()
    {
        var injector = BuildInjector(Environments.Production, "OrderCreatedIntegrationEvent");

        injector.ShouldCrashBeforeAck("OrderCreatedIntegrationEvent").Should().BeFalse();
    }

    [Fact]
    public void ShouldCrashBeforeAck_Should_ReturnFalse_When_EventTypeDoesNotMatch()
    {
        var injector = BuildInjector(Environments.Development, "OrderCreatedIntegrationEvent");

        injector.ShouldCrashBeforeAck("PaymentCapturedIntegrationEvent").Should().BeFalse();
    }

    [Fact]
    public void ShouldCrashBeforeAck_Should_ReturnFalse_When_ConfiguredEventTypeIsEmpty()
    {
        var injector = BuildInjector(Environments.Development, string.Empty);

        injector.ShouldCrashBeforeAck("OrderCreatedIntegrationEvent").Should().BeFalse();
    }

    [Fact]
    public void Crash_Should_InvokeProcessTerminator()
    {
        var terminator = new FakeProcessTerminator();
        var injector = new CrashBeforeAckFaultInjector(
            new FakeHostEnvironment(Environments.Development),
            Options.Create(new FaultInjectionOptions { CrashBeforeAckEventType = "OrderCreatedIntegrationEvent" }),
            terminator,
            NullLogger<CrashBeforeAckFaultInjector>.Instance);

        injector.Crash("OrderCreatedIntegrationEvent", "msg-1");

        terminator.TerminateCalled.Should().BeTrue();
    }

    private static CrashBeforeAckFaultInjector BuildInjector(string environmentName, string? configuredEventType) =>
        new(
            new FakeHostEnvironment(environmentName),
            Options.Create(new FaultInjectionOptions { CrashBeforeAckEventType = configuredEventType }),
            new FakeProcessTerminator(),
            NullLogger<CrashBeforeAckFaultInjector>.Instance);

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "ZIONShop.EventBus.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class FakeProcessTerminator : IProcessTerminator
    {
        public bool TerminateCalled { get; private set; }
        public int? ExitCode { get; private set; }

        public void Terminate(int exitCode)
        {
            TerminateCalled = true;
            ExitCode = exitCode;
        }
    }
}
