using ZIONShop.Realtime;

namespace ZIONShop.Chat.Tests.Fakes;

internal sealed class FakeRealtimeEventPublisher : IRealtimeEventPublisher
{
    public bool ShouldThrow { get; set; }
    public List<(Guid UserId, string Event, object Payload)> UserPublishes { get; } = new();
    public List<(string Event, object Payload)> StaffPublishes { get; } = new();

    public Task PublishToUserAsync(Guid userId, string eventName, object payload, CancellationToken ct = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated realtime publish failure");
        UserPublishes.Add((userId, eventName, payload));
        return Task.CompletedTask;
    }

    public Task PublishToStaffAsync(string eventName, object payload, CancellationToken ct = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("Simulated realtime publish failure");
        StaffPublishes.Add((eventName, payload));
        return Task.CompletedTask;
    }
}
