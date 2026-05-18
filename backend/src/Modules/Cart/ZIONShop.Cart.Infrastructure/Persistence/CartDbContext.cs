using Microsoft.EntityFrameworkCore;
using ZIONShop.Cart.Application.Interfaces;
using ZIONShop.Cart.Domain.Entities;
using DomainCart = ZIONShop.Cart.Domain.Entities.Cart;

namespace ZIONShop.Cart.Infrastructure.Persistence;

public class CartDbContext : DbContext, ICartUnitOfWork
{
    public const string Schema = "cart";

    public CartDbContext(DbContextOptions<CartDbContext> options) : base(options) { }

    public DbSet<DomainCart> Carts => Set<DomainCart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CartDbContext).Assembly);
    }

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}
