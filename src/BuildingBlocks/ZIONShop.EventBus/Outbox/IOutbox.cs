using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Abstractions;

namespace ZIONShop.EventBus.Outbox;

public interface IOutbox<TDbContext> where TDbContext : DbContext
{
    void Add(IntegrationEvent @event);
}
