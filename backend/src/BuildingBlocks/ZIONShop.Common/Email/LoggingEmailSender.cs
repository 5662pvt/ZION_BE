using Microsoft.Extensions.Logging;

namespace ZIONShop.Common.Email;

/// <summary>
/// Development fallback when Email:Host is not configured. OTP codes and other emails appear in the API console.
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
