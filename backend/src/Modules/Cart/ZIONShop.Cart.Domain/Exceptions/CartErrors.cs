using ZIONShop.SharedKernel.Results;

namespace ZIONShop.Cart.Domain.Exceptions;

public static class CartErrors
{
    public static readonly Error CartNotFound = Error.NotFound("Cart.CartNotFound", "Cart not found");
    public static readonly Error ItemNotFound = Error.NotFound("Cart.ItemNotFound", "Cart item not found");
    public static readonly Error ProductUnavailable = Error.Conflict("Cart.ProductUnavailable", "Product is not available");
    public static readonly Error InvalidQuantity = Error.Validation("Cart.InvalidQuantity", "Quantity must be positive");
    public static readonly Error CartFull = Error.Conflict("Cart.CartFull", "Cart has reached maximum item count");
}
