namespace LMS.NotificationWorker.Options;

public sealed class NotificationOptions
{
    public string FrontendBaseUrl { get; set; } = "https://app.lms.local";
    public string VerifyBaseUrl { get; set; } = "https://app.lms.local/verify";
    public string PlatformName { get; set; } = "LMS Platform";
}
