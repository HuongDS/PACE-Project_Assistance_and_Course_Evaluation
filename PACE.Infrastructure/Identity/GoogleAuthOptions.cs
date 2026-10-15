namespace PACE.Infrastructure.Identity;

public sealed class GoogleAuthOptions
{
    public const string SectionName = "GoogleAuth";
    public string ClientId { get; set; } = string.Empty;
}
