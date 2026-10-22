using ZIONShop.Contracts.Auth;

namespace ZIONShop.Chat.Tests.Fakes;

internal sealed class FakeUserContactReader : IUserContactReader
{
    private readonly Dictionary<Guid, UserContact> _contacts = new();

    public FakeUserContactReader With(UserContact contact)
    {
        _contacts[contact.UserId] = contact;
        return this;
    }

    public Task<UserContact?> GetAsync(Guid userId, CancellationToken ct = default) =>
        Task.FromResult(_contacts.TryGetValue(userId, out var contact) ? contact : null);
}
