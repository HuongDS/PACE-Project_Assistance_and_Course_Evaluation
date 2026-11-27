using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PACE.Application.Analysis;
using PACE.Application.Analysis.Abstractions;

namespace PACE.Infrastructure.Analysis;

/// <summary>GitHub REST API. Có Analysis:GitHubToken thì dùng; không có vẫn hỏi được repo công khai trong hạn mức thấp.</summary>
public sealed partial class GitHubDirectory(HttpClient http, IOptions<AnalysisOptions> options, ILogger<GitHubDirectory> logger) : IGitHubDirectory
{
    [GeneratedRegex(@"^https://github\.com/(?<owner>[A-Za-z0-9_.-]+)/(?<repo>[A-Za-z0-9_.-]+?)(?:\.git)?(?:[/#?].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex Repository();

    private HttpRequestMessage Request(string path)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/" + path);
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("PACE", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        if (!string.IsNullOrWhiteSpace(options.Value.GitHubToken))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Value.GitHubToken);
        return request;
    }

    public async Task<bool?> UserExistsAsync(string username, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.SendAsync(Request("users/" + Uri.EscapeDataString(username)), HttpCompletionOption.ResponseHeadersRead, ct);
            return response.StatusCode == HttpStatusCode.NotFound ? false : response.IsSuccessStatusCode ? true : null;
        }
        catch (Exception ex) when (ex is HttpRequestException || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("Không hỏi được GitHub: {Message}", ex.Message);
            return null;
        }
    }

    public async Task<IReadOnlyDictionary<string, string>> CommitLoginsAsync(string repoUrl, IReadOnlyList<string> shas, CancellationToken ct = default)
    {
        var logins = new Dictionary<string, string>(StringComparer.Ordinal);
        var match = Repository().Match(repoUrl.Trim());
        if (!match.Success) return logins;
        var path = $"repos/{match.Groups["owner"].Value}/{match.Groups["repo"].Value}/commits/";
        foreach (var sha in shas)
        {
            try
            {
                using var response = await http.SendAsync(Request(path + Uri.EscapeDataString(sha)), ct);
                // Hết hạn mức hoặc repo riêng không có quyền: dừng, đừng hỏi tiếp.
                if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.TooManyRequests or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound) break;
                if (!response.IsSuccessStatusCode) continue;
                using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
                if (document.RootElement.TryGetProperty("author", out var author) && author.ValueKind == JsonValueKind.Object
                    && author.TryGetProperty("login", out var login) && login.GetString() is { Length: > 0 } name)
                    logins[sha] = name;
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException || ex is OperationCanceledException && !ct.IsCancellationRequested)
            {
                logger.LogWarning("Không hỏi được GitHub: {Message}", ex.Message);
                break;
            }
        }
        return logins;
    }
}
