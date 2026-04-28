namespace LMS.Gateway.RateLimiting;

public sealed class RateLimitOptions
{
    public bool Enabled { get; set; } = true;
    public int PerTenantPerMinute { get; set; } = 600;
    public int AnonymousPerMinute { get; set; } = 60;
    public int WindowSeconds { get; set; } = 60;
}
