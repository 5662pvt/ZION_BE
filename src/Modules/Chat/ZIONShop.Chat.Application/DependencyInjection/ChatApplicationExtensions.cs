using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Chat.Application.DependencyInjection;

public static class ChatApplicationExtensions
{
    public static IServiceCollection AddChatApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        return services;
    }
}
