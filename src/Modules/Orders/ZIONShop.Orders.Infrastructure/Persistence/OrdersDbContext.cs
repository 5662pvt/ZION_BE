using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZIONShop.EventBus.Abstractions;
using ZIONShop.EventBus.Inbox;
using ZIONShop.EventBus.Outbox;
using ZIONShop.Orders.Application.Exceptions;
using ZIONShop.Orders.Application.Interfaces;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Infrastructure.Persistence.Configurations;

namespace ZIONShop.Orders.Infrastructure.Persistence;

public class OrdersDbContext : DbContext, IOrdersUnitOfWork
{
    public const string Schema = "orders";

    private readonly Outbox<OrdersDbContext> _outbox;

    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options)
    {
        _outbox = new Outbox<OrdersDbContext>(this);
    }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<OrderStatusHistory> OrderStatusHistories => Set<OrderStatusHistory>();
    public DbSet<OrderIdempotencyKey> OrderIdempotencyKeys => Set<OrderIdempotencyKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(OrdersDbContext).Assembly);
        modelBuilder.AddOutbox(Schema);
        modelBuilder.AddInbox(Schema);
    }

    public void AddOutboxEvent(IntegrationEvent @event) => _outbox.Add(@event);

    /// <summary>Translates a unique-violation on the idempotency-key index into an Application-level exception
    /// (<see cref="DuplicateIdempotencyKeyException"/>) so PlaceOrderCommandHandler never references Npgsql.</summary>
    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsIdempotencyKeyViolation(ex))
        {
            throw new DuplicateIdempotencyKeyException(ex);
        }
    }

    private static bool IsIdempotencyKeyViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" } pgEx &&
        pgEx.ConstraintName == OrderIdempotencyKeyConfiguration.UniqueIndexName;
}
