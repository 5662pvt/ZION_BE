namespace ZIONShop.Orders.Application.Exceptions;

/// <summary>
/// Thrown by <see cref="Interfaces.IOrdersUnitOfWork.SaveChangesAsync"/> when the save violates the partial
/// unique index that allows at most one non-Rejected <c>ReturnRequest</c> per order — i.e. a concurrent
/// request already created an active return request for this order. Infrastructure translates the
/// provider-specific unique-violation into this Application-level type so the handler never references
/// Npgsql directly.
/// </summary>
public sealed class DuplicateReturnRequestException : Exception
{
    public DuplicateReturnRequestException(Exception innerException)
        : base("An active return request already exists for this order.", innerException)
    {
    }
}
