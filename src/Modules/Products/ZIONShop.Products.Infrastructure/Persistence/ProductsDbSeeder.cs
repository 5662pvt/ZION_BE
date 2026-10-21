using Microsoft.EntityFrameworkCore;
using ZIONShop.Products.Domain.Entities;
using ZIONShop.Products.Domain.ValueObjects;

namespace ZIONShop.Products.Infrastructure.Persistence;

public static class ProductsDbSeeder
{
    public static async Task SeedAsync(ProductsDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.MigrateAsync(cancellationToken);

        if (!await db.Products.AnyAsync(cancellationToken))
        {
            var electronics = Category.Create("Electronics", "electronics", null, "Electronic devices and gadgets", 1);
            var apparel = Category.Create("Apparel", "apparel", null, "Clothing and accessories", 2);
            var books = Category.Create("Books", "books", null, "Books and magazines", 3);
            await db.Categories.AddRangeAsync(new[] { electronics, apparel, books }, cancellationToken);

            var seedProducts = new[]
            {
                CreateProduct("Wireless Headphones", "wireless-headphones", "SKU-WH-001", 3_490_000m, electronics.Id, "BoomAudio",
                    "Bluetooth 5.3 over-ear headphones with active noise cancellation.",
                    "https://picsum.photos/seed/wh001/600/600"),
                CreateProduct("Smartphone Stand", "smartphone-stand", "SKU-SS-002", 2_990_000m, electronics.Id, "Stedi",
                    "Aluminum adjustable smartphone stand.",
                    "https://picsum.photos/seed/ss002/600/600"),
                CreateProduct("Mechanical Keyboard 75%", "mechanical-keyboard-75", "SKU-MK-003", 4_990_000m, electronics.Id, "Keycraft",
                    "Hot-swappable 75% layout mechanical keyboard.",
                    "https://picsum.photos/seed/mk003/600/600"),
                CreateProduct("Classic Cotton Tee", "classic-cotton-tee", "SKU-CT-004", 199_000m, apparel.Id, "Plainly",
                    "100% organic cotton crew neck tee.",
                    "https://picsum.photos/seed/ct004/600/600"),
                CreateProduct("Slim Fit Jeans", "slim-fit-jeans", "SKU-SJ-005", 499_000m, apparel.Id, "Denim Co",
                    "Stretch slim fit denim jeans.",
                    "https://picsum.photos/seed/sj005/600/600"),
                CreateProduct("Domain-Driven Design", "domain-driven-design", "SKU-DDD-006", 349_000m, books.Id, "Addison-Wesley",
                    "Tackling Complexity in the Heart of Software by Eric Evans.",
                    "https://picsum.photos/seed/ddd006/600/600"),
                CreateProduct("Clean Architecture", "clean-architecture", "SKU-CA-007", 299_000m, books.Id, "Pearson",
                    "A Craftsman's Guide to Software Structure and Design by Robert C. Martin.",
                    "https://picsum.photos/seed/ca007/600/600"),
                CreateProduct("USB-C Hub 7-in-1", "usb-c-hub-7in1", "SKU-UC-008", 3_290_000m, electronics.Id, "PortPro",
                    "HDMI 4K, SD/microSD, 3x USB 3.0, PD 100W.",
                    "https://picsum.photos/seed/uc008/600/600"),
            };
            await db.Products.AddRangeAsync(seedProducts, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        await SeedVariantProductAsync(db, cancellationToken);
    }

    private static Product CreateProduct(string name, string slug, string sku, decimal price, Guid categoryId, string brand, string description, string imageUrl)
    {
        var p = Product.Create(name, slug, Sku.Create(sku), Money.Create(price, "VND"), categoryId, description, brand);
        p.AddImage(imageUrl, name, 0);
        p.Publish();
        return p;
    }

    private static async Task SeedVariantProductAsync(ProductsDbContext db, CancellationToken cancellationToken)
    {
        const string slug = "ao-thun-zion-basic";
        if (await db.Products.AnyAsync(p => p.Slug == slug, cancellationToken)) return;

        var apparel = await db.Categories.FirstOrDefaultAsync(c => c.Slug == "apparel", cancellationToken);
        var categoryId = apparel?.Id;
        if (apparel is null)
        {
            apparel = Category.Create("Apparel", "apparel", null, "Clothing and accessories", 2);
            await db.Categories.AddAsync(apparel, cancellationToken);
            categoryId = apparel.Id;
        }

        var product = Product.CreateDraft("Áo thun ZION Basic", slug, categoryId, "Áo thun cotton co giãn, nhiều màu và kích cỡ.", "ZION");

        var redImage = product.AddImage("https://picsum.photos/seed/zion-basic-red/600/600", "Áo thun ZION Basic - Đỏ", null);
        product.AddImage("https://picsum.photos/seed/zion-basic/600/600", "Áo thun ZION Basic", null);

        var colors = new[] { "Trắng", "Đen", "Đỏ" };
        var sizes = new[] { "S", "M", "L" };

        var options = new List<ProductOptionInput>
        {
            new("Màu sắc", colors),
            new("Kích cỡ", sizes)
        };

        var variants = new List<ProductVariantInput>();
        var colorCode = new Dictionary<string, string> { ["Trắng"] = "TRANG", ["Đen"] = "DEN", ["Đỏ"] = "DO" };
        foreach (var color in colors)
        {
            foreach (var size in sizes)
            {
                var price = 199_000m + (size == "L" ? 20_000m : 0m);
                var imageId = color == "Đỏ" ? redImage.Id : (Guid?)null;
                variants.Add(new ProductVariantInput(null, color, size, $"TEE-ZB-{colorCode[color]}-{size}", price, "VND", true, imageId));
            }
        }

        var setResult = product.SetVariants(options, variants);
        if (setResult.IsFailure) return;

        product.MarkCreated();
        var publishResult = product.Publish();
        if (publishResult.IsFailure) return;

        await db.Products.AddAsync(product, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }
}
