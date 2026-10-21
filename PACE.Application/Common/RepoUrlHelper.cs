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

    private static readonly HashSet<string> HostedRepoHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "github.com", "gitlab.com", "bitbucket.org"
    };

    /// <summary>
    /// Kiểm tra link repo và trả về lý do cụ thể nếu không dùng được, hoặc null nếu hợp lệ.
    /// Link trỏ vào trang người dùng/tổ chức (https://github.com/ten-to-chuc) thường là sinh viên quên phần tên repo,
    /// nên báo riêng thay vì để lỗi xuất hiện muộn ở bước clone.
    /// </summary>
    public static string? FindProblem(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !IsSupportedUrl(url)) return "Link repo không hợp lệ.";

        var uri = new Uri(url.Trim());
        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        if (!HostedRepoHosts.Contains(host)) return null;

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments.Length switch
        {
            0 => $"Link thiếu tên người dùng/tổ chức và tên repo (dạng https://{host}/<người dùng>/<repo>).",
            1 => $"Link thiếu tên repo: \"{segments[0]}\" chỉ là tên người dùng hoặc tổ chức (dạng https://{host}/{segments[0]}/<repo>).",
            _ => null
        };
    }

    /// <summary>
    /// Sinh tên bản sao của repository với số thứ tự (1), (2), ... dựa trên tên gốc và danh sách các tên đã có.
    /// Ví dụ: "Nhóm 1" -> "Nhóm 1 (1)"; nếu đã có "Nhóm 1 (1)" -> "Nhóm 1 (2)".
    /// </summary>
    public static string ComputeCopyName(string baseName, IEnumerable<string> existingNames)
    {
        if (string.IsNullOrWhiteSpace(baseName)) baseName = "Repository";
        var match = Regex.Match(baseName.Trim(), @"^(.*?)(?:\s*\((\d+)\))?$");
        var cleanBase = (match.Success && match.Groups[1].Success && !string.IsNullOrWhiteSpace(match.Groups[1].Value)
            ? match.Groups[1].Value
            : baseName).Trim();

        var pattern = new Regex(
            $@"^{Regex.Escape(cleanBase)}\s*\((\d+)\)$",
            RegexOptions.IgnoreCase);

        int maxNum = 0;
        foreach (var name in existingNames)
        {
            if (string.IsNullOrWhiteSpace(name)) continue;
            var trimmed = name.Trim();
            if (string.Equals(trimmed, cleanBase, StringComparison.OrdinalIgnoreCase))
            {
                maxNum = Math.Max(maxNum, 0);
            }
            var m = pattern.Match(trimmed);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var num))
            {
                if (num > maxNum) maxNum = num;
            }
        }
        return $"{cleanBase} ({maxNum + 1})";
    }
}
