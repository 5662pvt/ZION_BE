using Microsoft.EntityFrameworkCore;
using ZIONShop.Admin.Domain.Entities;

namespace ZIONShop.Admin.Infrastructure.Persistence;

public class AdminDbContext : DbContext
{
    public const string Schema = "admin";

    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AdminDbContext).Assembly);
    }
}
