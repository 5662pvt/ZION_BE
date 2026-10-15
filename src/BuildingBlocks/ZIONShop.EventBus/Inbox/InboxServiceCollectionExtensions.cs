using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.EventBus.Inbox;

public static class InboxServiceCollectionExtensions
{
    /// <summary>Registers an <see cref="IInboxAdminStore"/> for <typeparamref name="TDbContext"/> so the
    /// admin "Hệ thống" screens can list this module's inbox without the Admin/API layer referencing its
    /// Infrastructure project. Call once per module whose <c>OnModelCreating</c> calls
    /// <see cref="InboxModelBuilderExtensions.AddInbox"/>.</summary>
    public static IServiceCollection AddInboxAdmin<TDbContext>(this IServiceCollection services) where TDbContext : DbContext
    {
        services.AddScoped<IInboxAdminStore, InboxAdminStore<TDbContext>>();
        return services;
    }
}
