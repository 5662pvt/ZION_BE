using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;

namespace ZIONShop.EventBus.IntegrationTests.Infrastructure;

/// <summary>A row a test handler writes as a side effect, so the decorator's transaction tests can
/// assert on genuinely committed (or rolled back) state rather than an in-memory counter.</summary>
public sealed class TestEffect
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>Test outbox + inbox DbContext with a per-test schema so every test can share the one
/// Postgres container without colliding on table rows.</summary>
public sealed class EventBusTestDbContext : DbContext
{
    private readonly string _schema;

    public EventBusTestDbContext(DbContextOptions<EventBusTestDbContext> options, string schema) : base(options)
    {
        _schema = schema;
    }

    internal string Schema => _schema;

    public DbSet<TestEffect> Effects => Set<TestEffect>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.AddOutbox(_schema);
        modelBuilder.AddInbox(_schema);
        modelBuilder.Entity<TestEffect>(b =>
        {
            b.ToTable("test_effects", _schema);
            b.HasKey(x => x.Id);
        });
    }
}

/// <summary>
/// EF Core's default model cache key is just the DbContext's CLR type, ignoring constructor state --
/// so every <see cref="EventBusTestDbContext"/> instance in the same test process, regardless of its
/// own <c>_schema</c>, would otherwise reuse whichever schema happened to compile the model first,
/// silently collapsing every test's "isolated" schema onto one shared physical schema. Keying the
/// cache on the connection string's schema search path (baked into the options at construction)
/// instead compiles (and caches) one model per distinct schema.
/// </summary>
public sealed class SchemaAwareModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime) =>
        (context.GetType(), ((EventBusTestDbContext)context).Schema, designTime);
}
