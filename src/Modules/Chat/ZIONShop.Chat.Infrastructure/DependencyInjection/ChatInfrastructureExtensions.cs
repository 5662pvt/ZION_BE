using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ZIONShop.Chat.Application.Interfaces;
using ZIONShop.Chat.Application.Services;
using ZIONShop.Chat.Domain.Repositories;
using ZIONShop.Chat.Infrastructure.Persistence;
using ZIONShop.Chat.Infrastructure.Repositories;

namespace ZIONShop.Chat.Infrastructure.DependencyInjection;

public static class ChatInfrastructureExtensions
{
    public static IServiceCollection AddChatInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ChatDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ChatDbContext.Schema)));

        services.TryAddSingleton(TimeProvider.System);

        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IChatMessageRepository, ChatMessageRepository>();
        services.AddScoped<IChatUnitOfWork>(sp => sp.GetRequiredService<ChatDbContext>());

        services.AddScoped<ChatCardService>();
        services.AddScoped<ConversationProvisioningService>();

        return services;
    }
}
