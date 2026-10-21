namespace ZIONShop.Products.Domain.Entities;

public record ProductOptionInput(string Name, IReadOnlyList<string> Values);

public record ProductVariantInput(
    Guid? Id,
    string? Option1Value,
    string? Option2Value,
    string Sku,
    decimal Price,
    string Currency,
    bool IsActive,
    Guid? ImageId);
