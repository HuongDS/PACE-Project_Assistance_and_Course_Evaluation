using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using System.Text.RegularExpressions;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Common;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis;
using PACE.Application.Common.Exceptions;

namespace PACE.Infrastructure.Analysis;

internal sealed class ClonedRepository(string rootPath, string workingPath, Action<string> cleanup) : IClonedRepository
{
    public string RootPath { get; } = rootPath;
    public string WorkingPath { get; } = workingPath;

    public void Dispose() => cleanup(RootPath);
}

public sealed partial class GitCloneService(IOptions<AnalysisOptions> options, ILogger<GitCloneService> logger) : IGitCloneService
{
    private readonly AnalysisOptions _options = options.Value;

    [GeneratedRegex(@"^https?://(?<host>github\.com|gitlab\.com|bitbucket\.org)/(?<owner>[^/]+)/(?<repo>[^/#?]+?)(?:\.git)?(?:/(?:tree|src|-/tree)/(?<branch>[^/]+)(?:/(?<sub>[^#?]+))?)?/?(?:[#?].*)?$", RegexOptions.IgnoreCase)]
    private static partial Regex HostedUrl();

    [GeneratedRegex(@"://[^/@\s]+@")]
    private static partial Regex Credentials();

    public async Task<IClonedRepository> CloneAsync(string repoUrl, CancellationToken ct = default)
    {
        var (cloneUrl, branch, subFolder) = ParseUrl(repoUrl);
        var root = string.IsNullOrWhiteSpace(_options.CloneRoot)
            ? Path.Combine(Path.GetTempPath(), "pace_repos")
            : _options.CloneRoot;
        Directory.CreateDirectory(root);
        var target = Path.Combine(root, $"clone_{Guid.NewGuid():N}");

        var (exitCode, stdErr) = await RunGitAsync(["clone", "--depth", "1", .. BranchArgs(branch), "--", cloneUrl, target], ct);
        if (exitCode != 0 && branch is not null)
        {
            DeleteDirectory(target);
            (exitCode, stdErr) = await RunGitAsync(["clone", "--depth", "1", "--", cloneUrl, target], ct);
            subFolder = null;
        }

        if (exitCode != 0)
        {
            DeleteDirectory(target);
            var message = Credentials().Replace(stdErr, "://***@").Trim();
            throw new ExternalServiceException("git", string.IsNullOrWhiteSpace(message)
                ? $"Clone thất bại (exit code {exitCode})."
                : $"Clone thất bại: {message}. Nếu repo private, cấu hình Analysis:GitHubToken.");
        }

        var working = target;
        if (!string.IsNullOrWhiteSpace(subFolder))
        {
            var candidate = Path.GetFullPath(Path.Combine(target, subFolder.Replace('/', Path.DirectorySeparatorChar)));
            if (candidate.StartsWith(target, StringComparison.OrdinalIgnoreCase) && Directory.Exists(candidate)) working = candidate;
        }

        return new ClonedRepository(target, working, DeleteDirectory);
    }

    private static IEnumerable<string> BranchArgs(string? branch) => branch is null ? Array.Empty<string>() : new[] { "--branch", branch };

    private (string CloneUrl, string? Branch, string? SubFolder) ParseUrl(string repoUrl)
    {
        var url = repoUrl.Trim();
        var match = HostedUrl().Match(url);
        if (!match.Success) return (url, null, null);

        var host = match.Groups["host"].Value.ToLowerInvariant();
        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value;
        var branch = match.Groups["branch"].Success ? match.Groups["branch"].Value : null;
        var sub = match.Groups["sub"].Success ? match.Groups["sub"].Value.Trim('/') : null;

        var auth = host == "github.com" && !string.IsNullOrWhiteSpace(_options.GitHubToken)
            ? $"x-access-token:{_options.GitHubToken}@"
            : string.Empty;
        return ($"https://{auth}{host}/{owner}/{repo}.git", branch, sub);
    }

    private async Task<(int ExitCode, string StdErr)> RunGitAsync(IEnumerable<string> arguments, CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo(_options.GitExecutable)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var arg in arguments) startInfo.ArgumentList.Add(arg);
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "never";
        startInfo.Environment["GIT_ASKPASS"] = string.Empty;

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(10, _options.CloneTimeoutSeconds)));

        using var process = new Process { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Exception ex)
        {
            throw new ExternalServiceException("git", $"Không chạy được '{_options.GitExecutable}'. Máy chủ cần cài git và có trong PATH.", ex);
        }

        var stdOutTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stdErrTask = process.StandardError.ReadToEndAsync(timeout.Token);

        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await Task.WhenAll(stdOutTask, stdErrTask);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            if (ct.IsCancellationRequested) throw;
            throw new ExternalServiceException("git", $"Clone quá {_options.CloneTimeoutSeconds} giây, đã huỷ.");
        }

        return (process.ExitCode, await stdErrTask);
    }

    private void DeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Không xoá được thư mục clone {Path}", path);
        }
    }
}
