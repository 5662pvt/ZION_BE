using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZIONShop.Products.Application.Exceptions;
using ZIONShop.Products.Application.Interfaces;
using ZIONShop.Products.Domain.Entities;

namespace ZIONShop.Products.Infrastructure.Persistence;

public class ProductsDbContext : DbContext, IProductsUnitOfWork
{
    public const string Schema = "products";

    /// <summary>Name EF Core's default convention assigns the unique index backing ProductVariants.Sku
    /// (confirmed against the AddProductVariants migration's CreateIndex call).</summary>
    private const string SkuUniqueIndexName = "IX_ProductVariants_Sku";

    public ProductsDbContext(DbContextOptions<ProductsDbContext> options) : base(options) { }

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ProductsDbContext).Assembly);
    }

    /// <summary>Translates a unique-violation on the variant SKU index into an Application-level exception
    /// (<see cref="DuplicateVariantSkuException"/>) so handlers never reference Npgsql — a defensive backstop
    /// behind the handler-level SKU checks (see SetVariantsCommandHandler / CreateProductCommandHandler).</summary>
    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsSkuViolation(ex))
        {
            throw new DuplicateVariantSkuException(ex);
        }
    }

    private static bool IsSkuViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" } pgEx &&
        pgEx.ConstraintName == SkuUniqueIndexName;
}
