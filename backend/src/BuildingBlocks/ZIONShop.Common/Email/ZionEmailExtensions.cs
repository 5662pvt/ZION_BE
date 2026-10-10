using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ZIONShop.Common.Email;

public static class ZionEmailExtensions
{
    public static IServiceCollection AddZionEmail(this IServiceCollection services, IConfiguration configuration)
    {
        var emailSection = configuration.GetSection(EmailOptions.SectionName);
        services.Configure<EmailOptions>(emailSection);
        var emailHost = emailSection[nameof(EmailOptions.Host)];
        if (string.IsNullOrWhiteSpace(emailHost))
            services.AddScoped<IEmailSender, LoggingEmailSender>();
        else
            services.AddScoped<IEmailSender, SmtpEmailSender>();
        return services;
    }
}
