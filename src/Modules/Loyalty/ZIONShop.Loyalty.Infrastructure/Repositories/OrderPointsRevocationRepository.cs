using Microsoft.EntityFrameworkCore;
using ZIONShop.Loyalty.Domain.Entities;
using ZIONShop.Loyalty.Domain.Repositories;
using ZIONShop.Loyalty.Infrastructure.Persistence;

namespace ZIONShop.Loyalty.Infrastructure.Repositories;

public class OrderPointsRevocationRepository : IOrderPointsRevocationRepository
{
    private readonly LoyaltyDbContext _db;

    public OrderPointsRevocationRepository(LoyaltyDbContext db) => _db = db;

    public Task<bool> ExistsAsync(Guid orderId, CancellationToken cancellationToken = default) =>
        _db.OrderPointsRevocations.AnyAsync(r => r.OrderId == orderId, cancellationToken);

    public async Task AddAsync(OrderPointsRevocation revocation, CancellationToken cancellationToken = default) =>
        await _db.OrderPointsRevocations.AddAsync(revocation, cancellationToken);
}
