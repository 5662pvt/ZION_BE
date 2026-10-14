using ZIONShop.Products.Domain.Enums;
using ZIONShop.Products.Domain.Events;
using ZIONShop.Products.Domain.Exceptions;
using ZIONShop.Products.Domain.ValueObjects;
using ZIONShop.SharedKernel.Entities;
using ZIONShop.SharedKernel.Results;

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

    public ProductImage AddImage(string url, string? alt, string? publicId)
    {
        var nextOrder = _images.Count == 0 ? 0 : _images.Max(i => i.DisplayOrder) + 1;
        var image = ProductImage.Create(Id, url, alt, nextOrder, publicId);
        _images.Add(image);
        return image;
    }

    public Result<ProductImage> RemoveImage(Guid imageId)
    {
        var image = _images.FirstOrDefault(i => i.Id == imageId);
        if (image is null) return Result.Failure<ProductImage>(ProductsErrors.ImageNotFound);

        _images.Remove(image);
        return Result.Success(image);
    }

    public Result ReorderImages(IReadOnlyList<Guid> imageIds)
    {
        var currentIds = _images.Select(i => i.Id).ToHashSet();
        if (imageIds.Count != currentIds.Count || !currentIds.SetEquals(imageIds))
            return Result.Failure(ProductsErrors.InvalidImageOrder);

        for (var i = 0; i < imageIds.Count; i++)
            _images.First(img => img.Id == imageIds[i]).UpdateDisplayOrder(i);

        return Result.Success();
    }
}
