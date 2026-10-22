namespace ZIONShop.Chat.Application.Exceptions;

/// <summary>
/// Thrown by <see cref="Interfaces.IChatUnitOfWork.SaveChangesAsync"/> when the save violates the unique
/// index on Conversations.CustomerId -- a defensive backstop for a race between two concurrent first
/// GET/send requests from the same customer (see ConversationProvisioningService.GetOrCreateAsync).
/// Infrastructure translates the provider-specific unique-violation into this Application-level type so
/// the provisioning service never references Npgsql.
/// </summary>
public sealed class DuplicateConversationException : Exception
{
    public DuplicateConversationException(Exception innerException)
        : base("Conversation already exists for this customer.", innerException)
    {
    }
}
