using System.Text.RegularExpressions;

namespace PACE.Application.Common;

public static partial class RepoUrlHelper
{
    [GeneratedRegex(@"^https?://(github\.com|gitlab\.com|bitbucket\.org)/([^/]+)/([^/#?]+)", RegexOptions.IgnoreCase)]
    private static partial Regex HostedRepoRegex();

    public static string Normalize(string url)
    {
        var trimmed = url.Trim().TrimEnd('/');
        var match = HostedRepoRegex().Match(trimmed);
        if (match.Success)
        {
            var repo = match.Groups[3].Value;
            if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) repo = repo[..^4];
            return $"https://{match.Groups[1].Value.ToLowerInvariant()}/{match.Groups[2].Value}/{repo}";
        }

        return trimmed.EndsWith(".git", StringComparison.OrdinalIgnoreCase) ? trimmed[..^4] : trimmed;
    }

    public static string DeriveName(string url)
    {
        var match = HostedRepoRegex().Match(Normalize(url));
        if (match.Success) return $"{match.Groups[2].Value}/{match.Groups[3].Value}";

        var parts = Normalize(url).Split('/', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 0 ? parts[^1] : url;
    }

    public static bool IsSupportedUrl(string url)
        => Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
