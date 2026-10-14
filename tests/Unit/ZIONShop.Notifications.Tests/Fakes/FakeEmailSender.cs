using ZIONShop.Common.Email;

namespace ZIONShop.Notifications.Tests.Fakes;

public record SentEmail(string To, string Subject, string HtmlBody);

public class FakeEmailSender : IEmailSender
{
    public List<SentEmail> Sent { get; } = new();
    public bool ShouldThrow { get; set; }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow) throw new InvalidOperationException("SMTP unavailable");
        Sent.Add(new SentEmail(to, subject, htmlBody));
        return Task.CompletedTask;
    }
}
