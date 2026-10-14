using ZIONShop.Orders.Application.Interfaces;

namespace ZIONShop.Orders.Tests.Fakes;

public class FakeIdempotencyLock : IIdempotencyLock
{
    private bool _nextAcquireFails;

    public List<(Guid UserId, string Key)> AcquireCalls { get; } = new();

    public FakeIdempotencyLock WithNextAcquireFailing()
    {
        _nextAcquireFails = true;
        return this;
    }

    public Task<IAsyncDisposable?> AcquireAsync(Guid userId, string key, CancellationToken cancellationToken = default)
    {
        AcquireCalls.Add((userId, key));
        if (_nextAcquireFails)
        {
            _nextAcquireFails = false;
            return Task.FromResult<IAsyncDisposable?>(null);
        }

        return Task.FromResult<IAsyncDisposable?>(NoopReleaser.Instance);
    }

    private sealed class NoopReleaser : IAsyncDisposable
    {
        public static readonly NoopReleaser Instance = new();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
