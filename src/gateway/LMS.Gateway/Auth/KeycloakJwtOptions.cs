namespace LMS.Gateway.Auth;

public sealed class KeycloakJwtOptions
{
    public string Authority { get; set; } = string.Empty;
    public string Audience { get; set; } = "lms-api";
}
