using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace ZIONShop.EventBus.IntegrationTests.Infrastructure;

/// <summary>Captures every log entry across the whole host (every <c>ILogger&lt;T&gt;</c>) so a test
/// can assert on what was logged -- e.g. that no ObjectDisposedException ever surfaced -- instead of
/// just the NullLogger the other tests use.</summary>
public sealed class TestLogCapture
{
    public sealed record Entry(LogLevel Level, string Message, Exception? Exception);

    private readonly ConcurrentBag<Entry> _entries = new();

    public void Record(LogLevel level, string message, Exception? exception) =>
        _entries.Add(new Entry(level, message, exception));

    public IReadOnlyCollection<Entry> Entries => _entries.ToArray();

    public bool AnyObjectDisposedException => _entries.Any(e => ContainsObjectDisposed(e.Exception));

    private static bool ContainsObjectDisposed(Exception? exception)
    {
        for (var ex = exception; ex is not null; ex = ex.InnerException)
        {
            if (ex is ObjectDisposedException) return true;
        }
        return false;
    }
}

public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly TestLogCapture _capture;

    public CapturingLogger(TestLogCapture capture) => _capture = capture;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) =>
        _capture.Record(logLevel, formatter(state, exception), exception);
}
