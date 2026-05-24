using Microsoft.Extensions.Logging;
using ZIONShop.Auth.Application.Interfaces;

namespace ZIONShop.Auth.Infrastructure.Email;

/// <summary>
/// Development fallback when Email:Host is not configured. OTP codes appear in the API console.
/// </summary>
public class LoggingEmailSender : IEmailSender
{
    private readonly ILogger<LoggingEmailSender> _logger;

    public LoggingEmailSender(ILogger<LoggingEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning(
            "[DEV EMAIL — not sent via SMTP] To={To} Subject={Subject}{NewLine}{Body}",
            to,
            subject,
            Environment.NewLine,
            htmlBody);
        return Task.CompletedTask;
    }
}
