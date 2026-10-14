namespace ZIONShop.Orders.Application.Exceptions;

/// <summary>
/// Thrown by <see cref="Interfaces.IOrdersUnitOfWork.SaveChangesAsync"/> when the save violates the unique
/// (UserId, Key) index on the idempotency-key table — i.e. a concurrent request already won the race to
/// create the order for this Idempotency-Key. Infrastructure translates the provider-specific unique-violation
/// into this Application-level type so <c>PlaceOrderCommandHandler</c> never references Npgsql directly.
/// </summary>
public sealed class DuplicateIdempotencyKeyException : Exception
{
    public DuplicateIdempotencyKeyException(Exception innerException)
        : base("Idempotency-Key already used for another order.", innerException)
    {
    }
}
