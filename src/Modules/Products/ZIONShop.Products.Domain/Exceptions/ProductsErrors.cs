using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Domain.Exceptions;

public static class ProductsErrors
{
    public static readonly Error ProductNotFound = Error.NotFound("Products.ProductNotFound", "Product not found");
    public static readonly Error SkuAlreadyExists = Error.Conflict("Products.SkuAlreadyExists", "SKU already exists");
    public static readonly Error SlugAlreadyExists = Error.Conflict("Products.SlugAlreadyExists", "Slug already exists");
    public static readonly Error CategoryNotFound = Error.NotFound("Products.CategoryNotFound", "Category not found");
    public static readonly Error CategorySlugAlreadyExists = Error.Conflict("Products.CategorySlugAlreadyExists", "Category slug already exists");
    public static readonly Error CategoryHasChildren = Error.Conflict("Products.CategoryHasChildren", "Category has child categories");
    public static readonly Error CategoryHasProducts = Error.Conflict("Products.CategoryHasProducts", "Category has products assigned");
    public static readonly Error ImageNotFound = Error.NotFound("Products.ImageNotFound", "Product image not found");
    public static readonly Error InvalidImageOrder = Error.Validation("Products.InvalidImageOrder", "Image order must contain exactly the product's current image ids");
    public static readonly Error InvalidImageUpload = Error.Validation("Products.InvalidImageUpload", "Image upload failed");
    public static readonly Error InvalidSuggestQuery = Error.Validation("Products.InvalidSuggestQuery", "q must be between 2 and 100 characters");

    public static readonly Error TooManyOptions = Error.Validation("Products.TooManyOptions", "A product supports at most 2 option tiers");
    public static readonly Error InvalidOptionName = Error.Validation("Products.InvalidOptionName", "Option name must be between 1 and 30 characters");
    public static readonly Error DuplicateOptionName = Error.Validation("Products.DuplicateOptionName", "Option names must be unique");
    public static readonly Error InvalidOptionValues = Error.Validation("Products.InvalidOptionValues", "Each option must have between 1 and 20 distinct values, each 1-30 characters");
    public static readonly Error VariantValueNotInOption = Error.Validation("Products.VariantValueNotInOption", "Variant option values must match the product's declared option values");
    public static readonly Error DuplicateVariantCombination = Error.Validation("Products.DuplicateVariantCombination", "Active variants must have a unique combination of option values");
    public static readonly Error DuplicateVariantSku = Error.Validation("Products.DuplicateVariantSku", "Variant SKUs must be unique within the product");
    public static readonly Error InvalidVariantSku = Error.Validation("Products.InvalidVariantSku", "Variant SKU must be between 3 and 64 characters");
    public static readonly Error InvalidVariantPrice = Error.Validation("Products.InvalidVariantPrice", "Variant price is invalid");
    public static readonly Error VariantImageMismatch = Error.Validation("Products.VariantImageMismatch", "Variant image must belong to the same product");
    public static readonly Error NoActiveVariant = Error.Validation("Products.NoActiveVariant", "A product must have at least one active variant");
    public static readonly Error LegacyPriceUpdateNotAllowed = Error.Validation("Products.LegacyPriceUpdateNotAllowed", "Use the variants endpoint to update price for a product with options or multiple variants");
}
