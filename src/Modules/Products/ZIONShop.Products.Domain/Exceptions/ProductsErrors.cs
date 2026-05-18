using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Products.Domain.Exceptions;

public static class ProductsErrors
{
    public static readonly Error ProductNotFound = Error.NotFound("Products.ProductNotFound", "Product not found");
    public static readonly Error SkuAlreadyExists = Error.Conflict("Products.SkuAlreadyExists", "SKU already exists");
    public static readonly Error SlugAlreadyExists = Error.Conflict("Products.SlugAlreadyExists", "Slug already exists");
    public static readonly Error CategoryNotFound = Error.NotFound("Products.CategoryNotFound", "Category not found");
    public static readonly Error CategorySlugAlreadyExists = Error.Conflict("Products.CategorySlugAlreadyExists", "Category slug already exists");
}
