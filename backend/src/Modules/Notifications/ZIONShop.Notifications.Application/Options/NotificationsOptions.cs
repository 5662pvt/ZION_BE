namespace ZIONShop.Notifications.Application.Options;

public class NotificationsOptions
{
    public const string SectionName = "Notifications";

    public string StorefrontBaseUrl { get; set; } = "http://localhost:3000";
}
