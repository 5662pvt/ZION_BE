using ZIONShop.Chat.Application.Exceptions;
using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Application.Services;
using ZIONShop.Chat.Domain.Entities;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Chat.Tests.Fakes;
using ZIONShop.Chat.Tests.TestSupport;
using ZIONShop.Contracts.Auth;

namespace ZIONShop.Chat.Tests.Services;

public class ConversationProvisioningServiceTests
{
    private sealed class FakeConversationRepository : IConversationRepository
    {
        public Conversation? ExistingAfterRace { get; set; }

        private int _getByCustomerIdCalls;

        public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Conversation?>(null);

        public Task<Conversation?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
        {
            _getByCustomerIdCalls++;
            // 1st call: the initial existence check inside GetOrCreateAsync -- nothing created yet.
            // 2nd call: the retry after the simulated unique-violation -- the race winner's row is there.
            return Task.FromResult(_getByCustomerIdCalls == 1 ? null : ExistingAfterRace);
        }

        public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public IQueryable<Conversation> Query() => throw new NotSupportedException();
    }

    /// <summary>Throws <see cref="DuplicateConversationException"/> on the first SaveChangesAsync call
    /// (simulating a concurrent insert winning the unique-index race), then succeeds -- mirrors what
    /// ChatDbContext.SaveChangesAsync does against a real unique-violation from Postgres.</summary>
    private sealed class FakeUnitOfWorkThatRacesOnce : IChatUnitOfWork
    {
        private bool _thrown;
        public bool Detached { get; private set; }

        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_thrown)
            {
                _thrown = true;
                throw new DuplicateConversationException(new InvalidOperationException("simulated 23505 on IX_Conversations_CustomerId"));
            }

            return Task.FromResult(1);
        }

        public void Detach(object entity) => Detached = true;
    }

    [Fact]
    public async Task GetOrCreateAsync_Should_ReturnExistingConversation_When_ConcurrentCreateRacesOnUniqueCustomerIdIndex()
    {
        var customerId = Guid.NewGuid();
        var winner = Conversation.Create(customerId, "Nguyen Van A", "a@test.local", DateTime.UtcNow);
        var conversations = new FakeConversationRepository { ExistingAfterRace = winner };
        var uow = new FakeUnitOfWorkThatRacesOnce();
        var contacts = new FakeUserContactReader().With(new UserContact(customerId, "a@test.local", "Nguyen Van A"));
        var time = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var provisioning = new ConversationProvisioningService(conversations, contacts, uow, time);

        var result = await provisioning.GetOrCreateAsync(customerId, default);

        result.Should().BeSameAs(winner);
        uow.Detached.Should().BeTrue();
    }
}
