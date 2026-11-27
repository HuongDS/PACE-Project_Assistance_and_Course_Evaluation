using System.Globalization;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Common.Exceptions;

namespace PACE.Infrastructure.Analysis;

public sealed partial class GitCloneService : IGitHistoryService
{
    public async Task<GitRevision?> CheckoutBeforeAsync(IClonedRepository clone, DateTime? beforeUtc, CancellationToken ct = default)
    {
        var arguments = new List<string> { "-C", clone.RootPath, "log", "-1", "--format=%H %cI" };
        if (beforeUtc is DateTime before)
            arguments.Add("--before=" + before.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
        arguments.Add("HEAD");

        var found = await RunGitAsync(arguments, ct);
        if (found.ExitCode != 0) throw new ExternalServiceException("git", "Không đọc được lịch sử commit.");
        var parts = found.StdOut.Trim().Split(' ', 2);
        if (parts.Length < 2) return null;

        var checkout = await RunGitAsync(["-C", clone.RootPath, "checkout", "--detach", "--force", parts[0]], ct);
        if (checkout.ExitCode != 0) throw new ExternalServiceException("git", "Không chuyển được mã nguồn về commit cần đọc.");
        return new GitRevision(parts[0], DateTimeOffset.Parse(parts[1], CultureInfo.InvariantCulture).UtcDateTime);
    }

    public async Task<IReadOnlyList<GitLineAuthor>> BlameAsync(IClonedRepository clone, string relativePath, CancellationToken ct = default)
    {
        // -w: sửa khoảng trắng không làm đổi tác giả; -M: dời đoạn mã trong file vẫn tính cho người viết ban đầu.
        var result = await RunGitAsync(["-C", clone.WorkingPath, "blame", "--line-porcelain", "-w", "-M", "--", relativePath], ct);
        return result.ExitCode != 0 ? [] : ParseBlame(result.StdOut);
    }

    public static IReadOnlyList<GitLineAuthor> ParseBlame(string porcelain)
    {
        var lines = new List<GitLineAuthor>();
        string name = "", email = "", sha = "", summary = "";
        DateTime? at = null;
        var header = true; // dòng đầu của mỗi khối bắt đầu bằng mã commit
        foreach (var raw in porcelain.Split('\n'))
        {
            if (raw.StartsWith('\t'))
            {
                lines.Add(new GitLineAuthor(email, name, sha, at, summary));
                header = true;
                continue;
            }
            var line = raw.TrimEnd('\r');
            if (header) { sha = line.Split(' ')[0]; header = false; }
            else if (line.StartsWith("author ", StringComparison.Ordinal)) name = line["author ".Length..].Trim();
            else if (line.StartsWith("author-mail ", StringComparison.Ordinal)) email = line["author-mail ".Length..].Trim().Trim('<', '>');
            else if (line.StartsWith("author-time ", StringComparison.Ordinal) && long.TryParse(line["author-time ".Length..], out var seconds))
                at = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
            else if (line.StartsWith("summary ", StringComparison.Ordinal)) summary = line["summary ".Length..].Trim();
        }
        return lines;
    }
}
