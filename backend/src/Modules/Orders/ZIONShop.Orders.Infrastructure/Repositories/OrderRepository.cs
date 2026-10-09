using Microsoft.EntityFrameworkCore;
using ZIONShop.Orders.Domain.Entities;
using ZIONShop.Orders.Domain.Repositories;
using ZIONShop.Orders.Infrastructure.Persistence;

namespace ZIONShop.Orders.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _db;

    public OrderRepository(OrdersDbContext db) => _db = db;

    public Task<Order?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.Orders
            .Include(o => o.Lines)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public async Task AddAsync(Order order, CancellationToken cancellationToken = default) =>
        await _db.Orders.AddAsync(order, cancellationToken);

    public void Update(Order order)
    {
        // Same rationale as ZIONShop.Cart.Infrastructure.Repositories.CartRepository.Update: child entities
        // (OrderStatusHistory, OrderLine) carry client-generated Guid keys, so EF's own heuristics would
        // misclassify a newly-added entry on an already-tracked Order as Modified instead of Added once
        // DetectChanges runs. Suspend auto-detection and mark new children explicitly before SaveChanges.
        var wasAutoDetectEnabled = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var orderEntry = _db.Entry(order);
            if (orderEntry.State == EntityState.Detached)
            {
                _db.Orders.Update(order);
                return;
            }

            foreach (var line in order.Lines)
            {
                var lineEntry = _db.Entry(line);
                if (lineEntry.State == EntityState.Detached) lineEntry.State = EntityState.Added;
            }

            foreach (var history in order.StatusHistory)
            {
                var historyEntry = _db.Entry(history);
                if (historyEntry.State == EntityState.Detached) historyEntry.State = EntityState.Added;
            }
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = wasAutoDetectEnabled;
        }
    }

    public IQueryable<Order> Query() => _db.Orders.AsQueryable();
}
