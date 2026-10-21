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
    private readonly List<ProductVariant> _variants = new();
    private readonly List<ProductOption> _options = new();

    private Product() { }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public ProductStatus Status { get; private set; } = ProductStatus.Draft;
    public Guid? CategoryId { get; private set; }
    public string? Brand { get; private set; }
    public decimal MinPrice { get; private set; }
    public decimal MaxPrice { get; private set; }

    public IReadOnlyCollection<ProductImage> Images => _images.AsReadOnly();
    public IReadOnlyCollection<ProductVariant> Variants => _variants.AsReadOnly();
    public IReadOnlyCollection<ProductOption> Options => _options.AsReadOnly();

    /// <summary>The variant with no option values when the product has no options; falls back to display order otherwise.</summary>
    public ProductVariant? DefaultVariant => _variants.FirstOrDefault(v => v.Id == Id) ?? _variants.OrderBy(v => v.DisplayOrder).FirstOrDefault();

    /// <summary>Legacy creation path (no options). The single resulting variant's Id equals the product's own Id
    /// so that every downstream row already keyed by ProductId (stock/cart/order) remains a valid VariantId.</summary>
    public static Product Create(string name, string slug, Sku sku, Money price, Guid? categoryId, string? description, string? brand)
    {
        var product = CreateDraft(name, slug, categoryId, description, brand);
        product.InitializeDefaultVariant(sku, price);
        product.AddDomainEvent(new ProductCreatedDomainEvent(product.Id, sku.Value, product.Name));
        return product;
    }

    /// <summary>Shell with no variants/options yet. Caller must attach images (if any) then call
    /// <see cref="SetVariants"/> followed by <see cref="MarkCreated"/>.</summary>
    public static Product CreateDraft(string name, string slug, Guid? categoryId, string? description, string? brand) => new()
    {
        Id = Guid.NewGuid(),
        Name = name.Trim(),
        Slug = slug.Trim().ToLowerInvariant(),
        Description = description,
        CategoryId = categoryId,
        Brand = brand?.Trim()
    };

    private void InitializeDefaultVariant(Sku sku, Money price)
    {
        var variant = ProductVariant.Create(Id, Id, sku, price, null, null, null, true, 0);
        _variants.Add(variant);
        RecalculateMinMaxPrice();
    }

    /// <summary>Raises the creation event for products built via <see cref="CreateDraft"/> + <see cref="SetVariants"/>.</summary>
    public void MarkCreated()
    {
        var sku = _variants.OrderBy(v => v.DisplayOrder).Select(v => v.Sku.Value).FirstOrDefault() ?? string.Empty;
        AddDomainEvent(new ProductCreatedDomainEvent(Id, sku, Name));
    }

    public Result Update(string name, string? description, Money price, Guid? categoryId, string? brand)
    {
        if (!HasSingleDefaultVariantWithNoOptions())
            return Result.Failure(ProductsErrors.LegacyPriceUpdateNotAllowed);

        Name = name.Trim();
        Description = description;
        CategoryId = categoryId;
        Brand = brand?.Trim();

        var variant = _variants.First(v => v.IsActive);
        variant.UpdatePrice(price);
        RecalculateMinMaxPrice();

        return Result.Success();
    }

    private bool HasSingleDefaultVariantWithNoOptions() => _options.Count == 0 && _variants.Count(v => v.IsActive) == 1;

    public Result Publish()
    {
        if (!_variants.Any(v => v.IsActive))
            return Result.Failure(ProductsErrors.NoActiveVariant);

        Status = ProductStatus.Published;
        return Result.Success();
    }

    public void Archive() => Status = ProductStatus.Archived;

    /// <summary>Full replace of options + variants (matrix-editor semantics). Variants matched by Id keep their
    /// Id; variants omitted from the payload are deactivated, never deleted; new variants get new Guids.</summary>
    public Result SetVariants(IReadOnlyList<ProductOptionInput> optionInputs, IReadOnlyList<ProductVariantInput> variantInputs)
    {
        if (optionInputs.Count > 2)
            return Result.Failure(ProductsErrors.TooManyOptions);

        var trimmedOptions = new List<(string Name, List<string> Values)>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var opt in optionInputs)
        {
            var name = opt.Name?.Trim() ?? string.Empty;
            if (name.Length is < 1 or > 30)
                return Result.Failure(ProductsErrors.InvalidOptionName);
            if (!seenNames.Add(name))
                return Result.Failure(ProductsErrors.DuplicateOptionName);

            if (opt.Values is null || opt.Values.Count is < 1 or > 20)
                return Result.Failure(ProductsErrors.InvalidOptionValues);

            var values = new List<string>();
            var seenValues = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in opt.Values)
            {
                var value = raw?.Trim() ?? string.Empty;
                if (value.Length is < 1 or > 30)
                    return Result.Failure(ProductsErrors.InvalidOptionValues);
                if (!seenValues.Add(value))
                    return Result.Failure(ProductsErrors.InvalidOptionValues);
                values.Add(value);
            }
            trimmedOptions.Add((name, values));
        }

        if (variantInputs.Count == 0)
            return Result.Failure(ProductsErrors.NoActiveVariant);

        var option1Values = trimmedOptions.Count >= 1 ? trimmedOptions[0].Values.ToHashSet(StringComparer.OrdinalIgnoreCase) : null;
        var option2Values = trimmedOptions.Count >= 2 ? trimmedOptions[1].Values.ToHashSet(StringComparer.OrdinalIgnoreCase) : null;

        // First pass: validate everything and resolve each input against either an existing variant or a
        // new one, WITHOUT mutating any state yet, so a failure on a later input never leaves earlier
        // inputs partially applied.
        var matchedIds = new HashSet<Guid>();
        var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenActiveCombos = new HashSet<(string?, string?)>();
        var hasActiveVariant = false;
        var resolved = new List<(ProductVariant? Existing, Sku Sku, Money Price, string? Option1Value, string? Option2Value, Guid? ImageId, bool IsActive, int DisplayOrder)>();

        for (var i = 0; i < variantInputs.Count; i++)
        {
            var input = variantInputs[i];

            string? option1Value = null;
            if (option1Values is null)
            {
                if (!string.IsNullOrWhiteSpace(input.Option1Value))
                    return Result.Failure(ProductsErrors.VariantValueNotInOption);
            }
            else
            {
                option1Value = input.Option1Value?.Trim();
                if (string.IsNullOrWhiteSpace(option1Value) || !option1Values.Contains(option1Value))
                    return Result.Failure(ProductsErrors.VariantValueNotInOption);
            }

            string? option2Value = null;
            if (option2Values is null)
            {
                if (!string.IsNullOrWhiteSpace(input.Option2Value))
                    return Result.Failure(ProductsErrors.VariantValueNotInOption);
            }
            else
            {
                option2Value = input.Option2Value?.Trim();
                if (string.IsNullOrWhiteSpace(option2Value) || !option2Values.Contains(option2Value))
                    return Result.Failure(ProductsErrors.VariantValueNotInOption);
            }

            Sku skuVo;
            try { skuVo = Sku.Create(input.Sku); }
            catch (ArgumentException) { return Result.Failure(ProductsErrors.InvalidVariantSku); }

            if (!seenSkus.Add(skuVo.Value))
                return Result.Failure(ProductsErrors.DuplicateVariantSku);

            if (input.ImageId.HasValue && !_images.Any(img => img.Id == input.ImageId.Value))
                return Result.Failure(ProductsErrors.VariantImageMismatch);

            if (input.IsActive)
            {
                if (!seenActiveCombos.Add((option1Value, option2Value)))
                    return Result.Failure(ProductsErrors.DuplicateVariantCombination);
                hasActiveVariant = true;
            }

            Money priceVo;
            try { priceVo = Money.Create(input.Price, input.Currency); }
            catch (ArgumentException) { return Result.Failure(ProductsErrors.InvalidVariantPrice); }

            var existing = input.Id.HasValue ? _variants.FirstOrDefault(v => v.Id == input.Id.Value) : null;
            if (existing is not null)
                matchedIds.Add(existing.Id);

            resolved.Add((existing, skuVo, priceVo, option1Value, option2Value, input.ImageId, input.IsActive, i));
        }

        if (!hasActiveVariant)
            return Result.Failure(ProductsErrors.NoActiveVariant);

        // Second pass: validation passed for every input — now it is safe to apply the mutations.
        var existingIds = _variants.Select(v => v.Id).ToHashSet();
        foreach (var r in resolved)
        {
            if (r.Existing is not null)
                r.Existing.Update(r.Sku, r.Price, r.Option1Value, r.Option2Value, r.ImageId, r.IsActive, r.DisplayOrder);
            else
                _variants.Add(ProductVariant.Create(Guid.NewGuid(), Id, r.Sku, r.Price, r.Option1Value, r.Option2Value, r.ImageId, r.IsActive, r.DisplayOrder));
        }

        foreach (var variant in _variants.Where(v => existingIds.Contains(v.Id) && !matchedIds.Contains(v.Id)))
            variant.Deactivate();

        _options.Clear();
        for (var i = 0; i < trimmedOptions.Count; i++)
            _options.Add(ProductOption.Create(i + 1, trimmedOptions[i].Name, trimmedOptions[i].Values));

        RecalculateMinMaxPrice();
        return Result.Success();
    }

    private void RecalculateMinMaxPrice()
    {
        var activePrices = _variants.Where(v => v.IsActive).Select(v => v.Price.Amount).ToList();
        MinPrice = activePrices.Count > 0 ? activePrices.Min() : 0m;
        MaxPrice = activePrices.Count > 0 ? activePrices.Max() : 0m;
    }

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

        foreach (var variant in _variants.Where(v => v.ImageId == imageId))
            variant.ClearImage();

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
