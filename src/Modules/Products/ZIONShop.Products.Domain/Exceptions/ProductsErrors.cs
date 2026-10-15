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
}
