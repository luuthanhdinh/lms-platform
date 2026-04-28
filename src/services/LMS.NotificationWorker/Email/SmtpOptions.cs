namespace LMS.NotificationWorker.Email;

public sealed class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1025;
    public string FromAddress { get; set; } = "noreply@lms.local";
    public string FromName { get; set; } = "LMS Platform";
    public bool UseAuth { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool UseTls { get; set; } = false;
}
