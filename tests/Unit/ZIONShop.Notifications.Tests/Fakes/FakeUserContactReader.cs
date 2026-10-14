using ZIONShop.Contracts.Auth;

namespace ZIONShop.Notifications.Tests.Fakes;

public class FakeUserContactReader : IUserContactReader
{
    private readonly Dictionary<Guid, UserContact> _contacts = new();

    public FakeUserContactReader With(Guid userId, string email, string? displayName = null)
    {
        _contacts[userId] = new UserContact(userId, email, displayName);
        return this;
    }

    public Task<UserContact?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(_contacts.TryGetValue(userId, out var contact) ? contact : null);
}
