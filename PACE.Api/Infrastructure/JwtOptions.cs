namespace PACE.Api.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "PACE";
    public string Audience { get; set; } = "PACE.Client";
    public int ExpiresMinutes { get; set; } = 480;
}
