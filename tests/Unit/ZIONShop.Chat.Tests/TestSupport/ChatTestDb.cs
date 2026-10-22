using Microsoft.EntityFrameworkCore;
using ZIONShop.Chat.Infrastructure.Persistence;

namespace ZIONShop.Chat.Tests.TestSupport;

internal static class ChatTestDb
{
    public static ChatDbContext Create() =>
        new(new DbContextOptionsBuilder<ChatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);
}
