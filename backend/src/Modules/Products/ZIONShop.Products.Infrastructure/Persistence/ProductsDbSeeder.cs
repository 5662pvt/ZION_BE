using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;

namespace ZIONShop.Products.Infrastructure.Persistence;

public static class ProductsDbSeeder
{
    public static async Task SeedAsync(ProductsDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);
        if (await db.Products.AnyAsync(cancellationToken)) return;

        var electronics = Category.Create("Electronics", "electronics", null, "Electronic devices and gadgets", 1);
        var apparel = Category.Create("Apparel", "apparel", null, "Clothing and accessories", 2);
        var books = Category.Create("Books", "books", null, "Books and magazines", 3);
        await db.Categories.AddRangeAsync(new[] { electronics, apparel, books }, cancellationToken);

        var seedProducts = new[]
        {
            CreateProduct("Wireless Headphones", "wireless-headphones", "SKU-WH-001", 89.90m, electronics.Id, "BoomAudio",
                "Bluetooth 5.3 over-ear headphones with active noise cancellation.",
                "https://picsum.photos/seed/wh001/600/600"),
            CreateProduct("Smartphone Stand", "smartphone-stand", "SKU-SS-002", 14.50m, electronics.Id, "Stedi",
                "Aluminum adjustable smartphone stand.",
                "https://picsum.photos/seed/ss002/600/600"),
            CreateProduct("Mechanical Keyboard 75%", "mechanical-keyboard-75", "SKU-MK-003", 129.00m, electronics.Id, "Keycraft",
                "Hot-swappable 75% layout mechanical keyboard.",
                "https://picsum.photos/seed/mk003/600/600"),
            CreateProduct("Classic Cotton Tee", "classic-cotton-tee", "SKU-CT-004", 19.99m, apparel.Id, "Plainly",
                "100% organic cotton crew neck tee.",
                "https://picsum.photos/seed/ct004/600/600"),
            CreateProduct("Slim Fit Jeans", "slim-fit-jeans", "SKU-SJ-005", 49.00m, apparel.Id, "Denim Co",
                "Stretch slim fit denim jeans.",
                "https://picsum.photos/seed/sj005/600/600"),
            CreateProduct("Domain-Driven Design", "domain-driven-design", "SKU-DDD-006", 39.99m, books.Id, "Addison-Wesley",
                "Tackling Complexity in the Heart of Software by Eric Evans.",
                "https://picsum.photos/seed/ddd006/600/600"),
            CreateProduct("Clean Architecture", "clean-architecture", "SKU-CA-007", 32.50m, books.Id, "Pearson",
                "A Craftsman's Guide to Software Structure and Design by Robert C. Martin.",
                "https://picsum.photos/seed/ca007/600/600"),
            CreateProduct("USB-C Hub 7-in-1", "usb-c-hub-7in1", "SKU-UC-008", 39.00m, electronics.Id, "PortPro",
                "HDMI 4K, SD/microSD, 3x USB 3.0, PD 100W.",
                "https://picsum.photos/seed/uc008/600/600"),
        };
        await db.Products.AddRangeAsync(seedProducts, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Product CreateProduct(string name, string slug, string sku, decimal price, Guid categoryId, string brand, string description, string imageUrl)
    {
        var p = Product.Create(name, slug, Sku.Create(sku), Money.Create(price, "USD"), categoryId, description, brand);
        p.AddImage(imageUrl, name, 0);
        p.Publish();
        return p;
    }
}
