using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZIONShop.EventBus.FaultInjection;

namespace ZIONShop.EventBus.Tests.FaultInjection;

public class FaultInjectionStartupLoggerTests
{
    [Fact]
    public async Task StartAsync_Should_LogWarning_When_NotDevelopmentAndEventTypeIsSet()
    {
        var logger = new RecordingLogger<FaultInjectionStartupLogger>();
        var sut = new FaultInjectionStartupLogger(
            new FakeHostEnvironment(Environments.Production),
            Options.Create(new FaultInjectionOptions { CrashBeforeAckEventType = "OrderCreatedIntegrationEvent" }),
            logger);

        await sut.StartAsync(CancellationToken.None);

        logger.Entries.Should().ContainSingle(e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task StartAsync_Should_NotLog_When_EventTypeIsNotSet()
    {
        var logger = new RecordingLogger<FaultInjectionStartupLogger>();
        var sut = new FaultInjectionStartupLogger(
            new FakeHostEnvironment(Environments.Production),
            Options.Create(new FaultInjectionOptions()),
            logger);

        await sut.StartAsync(CancellationToken.None);

        logger.Entries.Should().BeEmpty();
    }

    [Fact]
    public async Task StartAsync_Should_NotLog_When_Development()
    {
        var logger = new RecordingLogger<FaultInjectionStartupLogger>();
        var sut = new FaultInjectionStartupLogger(
            new FakeHostEnvironment(Environments.Development),
            Options.Create(new FaultInjectionOptions { CrashBeforeAckEventType = "OrderCreatedIntegrationEvent" }),
            logger);

        await sut.StartAsync(CancellationToken.None);

        logger.Entries.Should().BeEmpty();
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "ZIONShop.EventBus.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
