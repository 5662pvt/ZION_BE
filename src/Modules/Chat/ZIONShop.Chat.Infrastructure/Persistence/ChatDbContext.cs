using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Domain.Entities;

namespace ZIONShop.Chat.Infrastructure.Persistence;

public class ChatDbContext : DbContext, IChatUnitOfWork
{
    public const string Schema = "chat";

    public ChatDbContext(DbContextOptions<ChatDbContext> options) : base(options) { }

    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChatDbContext).Assembly);
    }

    public new Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}
