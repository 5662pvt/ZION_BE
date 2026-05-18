using ZIONShop.Products.Domain.Enums;
using ZIONShop.Products.Domain.Events;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.SharedKernel.Entities;

namespace ZIONShop.Products.Domain.Entities;

public class Product : AggregateRoot
{
    private readonly List<ProductImage> _images = new();

    private Product() { }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Sku Sku { get; private set; } = default!;
    public Money Price { get; private set; } = default!;
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public Guid? CategoryId { get; private set; }
    public string? Brand { get; private set; }

    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();

    public static Product Create(string name, string slug, Sku sku, Money price, Guid? categoryId, string? description, string? brand)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = name.Trim(),
            Slug = slug.Trim().ToLowerInvariant(),
            Description = description,
            Sku = sku,
            Price = price,
            CategoryId = categoryId,
            Brand = brand?.Trim()
        };
        product.AddDomainEvent(new ProductCreatedDomainEvent(product.Id, product.Sku.Value, product.Name));
        return product;
    }

    public void Update(string name, string? description, Money price, Guid? categoryId, string? brand)
    {
        Name = name.Trim();
        Description = description;
        Price = price;
        CategoryId = categoryId;
        Brand = brand?.Trim();
    }

    public void Publish() => Status = ProductStatus.Published;
    public void Archive() => Status = ProductStatus.Archived;

    public void AddImage(string url, string? alt, int displayOrder)
        => _images.Add(ProductImage.Create(Id, url, alt, displayOrder));
}
