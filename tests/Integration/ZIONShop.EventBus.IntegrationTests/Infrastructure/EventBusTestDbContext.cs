using Microsoft.EntityFrameworkCore;
using ZIONShop.EventBus.Outbox;

namespace ZIONShop.EventBus.IntegrationTests.Infrastructure;

/// <summary>Test outbox DbContext with a per-test schema so every test can share the one Postgres
/// container without colliding on table rows.</summary>
public sealed class EventBusTestDbContext : DbContext
{
    private readonly string _schema;

    public EventBusTestDbContext(DbContextOptions<EventBusTestDbContext> options, string schema) : base(options)
    {
        _schema = schema;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.AddOutbox(_schema);
}
