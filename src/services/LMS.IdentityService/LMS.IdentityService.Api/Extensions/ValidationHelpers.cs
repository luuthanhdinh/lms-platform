namespace LMS.IdentityService.Api.Extensions;

public static class ValidationHelpers
{
    private static readonly HashSet<string> ValidLanguages =
        new(["vi", "en", "fr", "de", "es", "ja", "ko", "zh"], StringComparer.OrdinalIgnoreCase);

    public static bool IsValidIanaTimezone(string? tz)
    {
        if (string.IsNullOrWhiteSpace(tz)) return false;
        try { TimeZoneInfo.FindSystemTimeZoneById(tz); return true; }
        catch { return false; }
    }

    public static bool IsValidLanguage(string? lang) =>
        !string.IsNullOrWhiteSpace(lang) && ValidLanguages.Contains(lang);

    public static bool IsValidHttpsUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return true; // nullable fields
        return Uri.TryCreate(url, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps;
    }
}
