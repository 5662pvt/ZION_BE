using Microsoft.EntityFrameworkCore;
using Npgsql;
using ZIONShop.Chat.Application.Exceptions;
using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Domain.Entities;

namespace ZIONShop.Chat.Infrastructure.Persistence;

public class ChatDbContext : DbContext, IChatUnitOfWork
{
    public const string Schema = "chat";

    /// <summary>Name EF Core's default convention assigns the unique index backing
    /// Conversations.CustomerId (confirmed against the InitialChat migration's CreateIndex call).</summary>
    private const string CustomerIdUniqueIndexName = "IX_Conversations_CustomerId";

    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
    }

    /// <summary>Translates a unique-violation on Conversations.CustomerId into an Application-level
    /// exception (<see cref="DuplicateConversationException"/>) so ConversationProvisioningService never
    /// references Npgsql -- a defensive backstop for a race between two concurrent first GET/send
    /// requests from the same customer.</summary>
    public new async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsCustomerIdViolation(ex))
        {
            throw new DuplicateConversationException(ex);
        }
    }

    public void Detach(object entity) => Entry(entity).State = EntityState.Detached;

    private static bool IsCustomerIdViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: "23505" } pgEx &&
        pgEx.ConstraintName == CustomerIdUniqueIndexName;
}
