using ZIONShop.Contracts.Auth;

namespace ZIONShop.Notifications.Tests.Fakes;

public class FakeStaffUserReader : IStaffUserReader
{
    private readonly List<StaffUser> _staffUsers = new();

    public FakeStaffUserReader With(params Guid[] userIds)
    {
        _staffUsers.AddRange(userIds.Select(id => new StaffUser(id)));
        return this;
    }

    public Task<IReadOnlyList<StaffUser>> GetAdminAndStaffUserIdsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<StaffUser>>(_staffUsers);
}
