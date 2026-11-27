using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Models;
using PACE.Application.Common.Exceptions;

namespace PACE.Application.Oral;

/// <summary>Một tài khoản git xuất hiện trong mã nguồn. <see cref="StudentId"/> là sinh viên được nhận ra (hoặc giảng viên chọn).</summary>
public record SnapshotAuthor(string Key, string Name, string Email, int Lines, Guid? StudentId, string? MatchedBy);

/// <summary>Một commit đã viết ra ít nhất một dòng của hàm.</summary>
public record SnapshotCommit(string Sha, string Message, DateTime? At, string AuthorKey);

/// <summary>
/// Một hàm tại commit đã chụp. <paramref name="LineOwners"/> ứng với từng dòng của <paramref name="Code"/>:
/// vị trí của tác giả trong <paramref name="LineAuthors"/>, hoặc -1 nếu dòng chỉ có ngoặc, chú thích hay để trống.
/// </summary>
public record SnapshotMethod(string Key, string File, string Name, int StartLine, string Code,
    List<string> LineAuthors, List<int> LineOwners, List<SnapshotCommit> Commits);

public record CodeSnapshot(List<SnapshotAuthor> Authors, List<SnapshotMethod> Methods, List<string> Warnings);

public record RosterStudent(Guid Id, string Code, string FullName, string Email);

public record ReadSnapshot(GitRevision Revision, CodeSnapshot Snapshot);

/// <summary>Hàm mà một sinh viên thật sự viết: những dòng nào (số dòng trong file) và chiếm bao nhiêu phần.</summary>
public record OwnedMethod(SnapshotMethod Method, List<int> OwnLines, double Share, int Score, List<SnapshotCommit> OwnCommits);

public interface ICodeSnapshotReader
{
    Task<ReadSnapshot> ReadAsync(string repoUrl, DateTime? beforeUtc, CancellationToken ct = default);
}

/// <summary>Chụp mã nguồn tại mốc hạn và ghi ai viết từng dòng. Không ghi gì vào dữ liệu phân tích đã có.</summary>
public partial class CodeSnapshotReader(IGitCloneService git, IGitHistoryService history, IRepositoryScanner scanner, IEnumerable<ILanguageParser> parsers) : ICodeSnapshotReader
{
    public const int MaxFiles = 400;
    public const int MaxMethodsPerAuthor = 60;
    public const int MaxCodeLength = 5000;
    public const int MaxCodeLines = 150;
    private const int MinLines = 4;
    /// <summary>Một commit chiếm quá phần này số dòng thì lịch sử không đáng tin để biết ai viết.</summary>
    private const double BulkShare = 0.4;

    [GeneratedRegex(@"^(\s*[{}()\[\];,]*\s*|\s*(//|/\*|\*|#(?!region|if)).*)$")]
    private static partial Regex Trivial();

    public static bool IsMeaningful(string line) => !Trivial().IsMatch(line);

    public async Task<ReadSnapshot> ReadAsync(string repoUrl, DateTime? beforeUtc, CancellationToken ct = default)
    {
        using var clone = await git.CloneAsync(repoUrl, ct);
        var revision = await history.CheckoutBeforeAsync(clone, beforeUtc, ct)
            ?? throw new UnprocessableException("Repo chưa có commit nào trước mốc hạn này.");
        if (!Directory.Exists(clone.WorkingPath))
            throw new UnprocessableException("Thư mục mã nguồn chưa tồn tại ở mốc hạn này.");

        var scan = scanner.Scan(clone.WorkingPath, ct);
        var parsed = new ParseResult();
        foreach (var parser in parsers)
            parsed.Merge(parser.Parse(scan.FilesFor(parser.Kind), ct));
        var covered = parsed.Methods.Select(m => m.FilePath).ToHashSet(StringComparer.Ordinal);
        parsed.Methods.AddRange(scan.FrontendEntries().Where(m => !covered.Contains(m.FilePath)));

        var warnings = new List<string>(scan.Warnings);
        var candidates = parsed.Methods
            .Where(m => !m.IsAbstract && !IsGenerated(m.FilePath) && LineCount(m.SourceCode) >= MinLines)
            .ToList();
        if (candidates.Count == 0)
            throw new UnprocessableException("Không tìm thấy hàm nào trong mã nguồn ở mốc hạn này.");

        // Mỗi file là một lần gọi git: chạy song song vài file một lúc để repo lớn không phải chờ lâu.
        var allFiles = candidates.GroupBy(m => m.FilePath, StringComparer.Ordinal)
            .OrderByDescending(g => g.Sum(m => LineCount(m.SourceCode))).Select(g => g.Key).ToList();
        if (allFiles.Count > MaxFiles) warnings.Add($"Repo lớn: chỉ xét {MaxFiles} file nhiều mã nhất trong {allFiles.Count} file.");
        var blame = new ConcurrentDictionary<string, IReadOnlyList<GitLineAuthor>>(StringComparer.Ordinal);
        await Parallel.ForEachAsync(allFiles.Take(MaxFiles), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (path, token) => blame[path] = await history.BlameAsync(clone, path, token));

        var methods = new List<SnapshotMethod>();
        var authors = new Dictionary<string, (string Name, string Email, int Lines)>(StringComparer.Ordinal);
        var perCommit = new Dictionary<string, (string Author, int Lines)>(StringComparer.Ordinal);
        var counted = new HashSet<(string, int)>(); // hàm lồng nhau không được đếm một dòng hai lần
        foreach (var method in candidates.OrderBy(m => m.FilePath, StringComparer.Ordinal).ThenBy(m => m.StartLine))
        {
            if (!blame.TryGetValue(method.FilePath, out var file)) continue;
            var code = Trim(method.SourceCode);
            var lineAuthors = new List<string>();
            var owners = new List<int>(code.Length);
            var commits = new Dictionary<string, SnapshotCommit>(StringComparer.Ordinal);
            var first = Math.Max(0, method.StartLine - 1);
            for (var i = 0; i < code.Length; i++)
            {
                var at = first + i;
                if (at >= file.Count || !IsMeaningful(code[i])) { owners.Add(-1); continue; }
                var line = file[at];
                var key = AuthorKey(line);
                var index = lineAuthors.IndexOf(key);
                if (index < 0) { index = lineAuthors.Count; lineAuthors.Add(key); }
                owners.Add(index);
                if (line.Sha.Length > 0) commits.TryAdd(line.Sha, new SnapshotCommit(line.Sha, Cut(line.Summary, 160), line.At, key));
                if (!counted.Add((method.FilePath, at))) continue;
                var seen = authors.GetValueOrDefault(key, (Name: line.Name, Email: line.Email, Lines: 0));
                authors[key] = (seen.Name, seen.Email, seen.Lines + 1);
                if (line.Sha.Length > 0) perCommit[line.Sha] = (line.Name, perCommit.GetValueOrDefault(line.Sha).Lines + 1);
            }
            if (lineAuthors.Count == 0) continue;
            methods.Add(new SnapshotMethod($"{method.FilePath}:{method.StartLine}:{method.Label}", method.FilePath, method.Label, method.StartLine,
                string.Join('\n', code), lineAuthors, owners, commits.Values.OrderByDescending(c => c.At).Take(12).ToList()));
        }

        // Giữ lại phần nhiều nhất của từng người, để người viết hàm ngắn không bị người viết hàm dài chiếm hết chỗ.
        var keep = new HashSet<string>(StringComparer.Ordinal);
        foreach (var author in authors.Keys)
            foreach (var method in methods
                .Select(m => (m.Key, Own: Own(m, author)))
                .Where(m => m.Own > 0).OrderByDescending(m => m.Own).Take(MaxMethodsPerAuthor))
                keep.Add(method.Key);
        methods = methods.Where(m => keep.Contains(m.Key)).ToList();

        var total = perCommit.Values.Sum(c => c.Lines);
        var biggest = perCommit.OrderByDescending(c => c.Value.Lines).FirstOrDefault();
        if (total >= 200 && biggest.Value.Lines > total * BulkShare)
            warnings.Insert(0, $"Một commit ({biggest.Key[..Math.Min(8, biggest.Key.Length)]}, {biggest.Value.Author}) chiếm {biggest.Value.Lines * 100 / total}% số dòng mã. "
                + "Mã có thể được đẩy một lần hoặc gộp lịch sử, nên hãy kiểm lại ai thật sự viết.");

        var list = authors.OrderByDescending(a => a.Value.Lines)
            .Select(a => new SnapshotAuthor(a.Key, a.Value.Name, a.Value.Email, a.Value.Lines, null, null)).ToList();
        return new ReadSnapshot(revision, new CodeSnapshot(list, methods, warnings));
    }

    private static int Own(SnapshotMethod method, string author)
    {
        var index = method.LineAuthors.IndexOf(author);
        return index < 0 ? 0 : method.LineOwners.Count(o => o == index);
    }

    /// <summary>Cắt theo cả dòng để số dòng luôn khớp với mảng tác giả.</summary>
    private static string[] Trim(string source)
    {
        var lines = source.Replace("\r", "").TrimEnd().Split('\n');
        var kept = 0;
        for (var length = 0; kept < lines.Length && kept < MaxCodeLines; kept++)
        {
            length += lines[kept].Length + 1;
            if (length > MaxCodeLength && kept > 0) break;
        }
        return lines.Take(kept).Select(l => Cut(l, MaxCodeLength)).ToArray();
    }

    private static string Cut(string text, int length) => text.Length > length ? text[..length] : text;

    private static string AuthorKey(GitLineAuthor author)
        => (string.IsNullOrWhiteSpace(author.Email) ? author.Name : author.Email).Trim().ToLowerInvariant();

    private static int LineCount(string code) => code.TrimEnd().Count(c => c == '\n') + 1;

    public static bool IsGenerated(string path)
    {
        var p = "/" + path.Replace('\\', '/').ToLowerInvariant();
        return p.Contains("/migrations/") || p.Contains("/node_modules/") || p.Contains("/dist/") || p.Contains("/build/")
            || p.EndsWith(".designer.cs") || p.EndsWith(".g.cs") || p.EndsWith(".min.js");
    }
}

/// <summary>Nối tài khoản git với sinh viên trong nhóm và chọn phần mã của từng người.</summary>
public static partial class OralOwnership
{
    /// <summary>Hàm được hỏi một sinh viên khi bạn ấy viết ít nhất chừng này dòng có nghĩa…</summary>
    public const int MinOwnLines = 3;
    /// <summary>…và hoặc chiếm quá nửa hàm, hoặc viết hẳn một đoạn đủ dài trong đó.</summary>
    public const int SubstantialLines = 8;
    public const int PromptMethods = 10;
    public const int PromptCharacters = 28000;

    [GeneratedRegex(@"\b(if|for|foreach|while|switch|case|catch|throw)\b")]
    private static partial Regex Branches();

    public static List<SnapshotAuthor> Match(IEnumerable<SnapshotAuthor> authors, IReadOnlyList<RosterStudent> roster)
        => authors.Select(author =>
        {
            var email = author.Email.Trim().ToLowerInvariant();
            var local = email.Split('@')[0];
            var name = Plain(author.Name);
            var rules = new (string Reason, Func<RosterStudent, bool> Fits)[]
            {
                ("trùng email", s => email.Length > 0 && s.Email.Trim().Equals(email, StringComparison.OrdinalIgnoreCase)),
                ("có mã số sinh viên", s => s.Code.Length >= 6 && (local.Contains(s.Code.ToLowerInvariant()) || name.Contains(s.Code.ToLowerInvariant()))),
                ("trùng họ tên", s => Words(s.FullName).Length >= 2 && (Words(s.FullName).SequenceEqual(Words(author.Name))
                    || Plain(s.FullName).Replace(" ", "") == local.Replace(".", "").Replace("_", ""))),
            };
            foreach (var (reason, fits) in rules)
            {
                var found = roster.Where(fits).Take(2).ToList();
                if (found.Count == 1) return author with { StudentId = found[0].Id, MatchedBy = reason };
            }
            return author with { StudentId = null, MatchedBy = null };
        }).ToList();

    /// <summary>Phần mã của một sinh viên (gộp mọi tài khoản git của bạn ấy), phần đáng hỏi đứng trước.</summary>
    public static List<OwnedMethod> OwnedBy(CodeSnapshot snapshot, Guid studentId)
    {
        var mine = snapshot.Authors.Where(a => a.StudentId == studentId).Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
        var result = new List<OwnedMethod>();
        foreach (var method in snapshot.Methods)
        {
            // Bản chụp cũ không ghi từng dòng thì phải đọc lại mã.
            if (method.LineOwners is null || method.LineAuthors is null) continue;
            var code = method.Code.Split('\n');
            var own = new List<int>();
            var meaningful = 0;
            var branches = 0;
            for (var i = 0; i < method.LineOwners.Count && i < code.Length; i++)
            {
                var owner = method.LineOwners[i];
                if (owner < 0) continue;
                meaningful++;
                if (!mine.Contains(method.LineAuthors[owner])) continue;
                own.Add(method.StartLine + i);
                branches += Branches().Matches(code[i]).Count;
            }
            if (own.Count < MinOwnLines) continue;
            var share = (double)own.Count / meaningful;
            if (share < 0.5 && own.Count < SubstantialLines) continue;
            result.Add(new OwnedMethod(method, own, share, (int)(own.Count * (0.5 + share) * (1 + Math.Min(6, branches))),
                (method.Commits ?? []).Where(c => mine.Contains(c.AuthorKey)).ToList()));
        }
        return result.OrderByDescending(r => r.Score).ThenBy(r => r.Method.Key, StringComparer.Ordinal).ToList();
    }

    /// <summary>Phần đưa cho AI: hàm chưa được hỏi trước, trải ra nhiều file rồi mới lấy thêm hàm cùng file.</summary>
    public static List<OwnedMethod> ForPrompt(IReadOnlyList<OwnedMethod> owned, ISet<string>? asked = null)
    {
        var ordered = owned.GroupBy(o => o.Method.File).SelectMany(g => g.Select((o, i) => (Owned: o, Round: i / 2)))
            .OrderBy(o => asked?.Contains(o.Owned.Method.Key) == true).ThenBy(o => o.Round).ThenByDescending(o => o.Owned.Score)
            .Select(o => o.Owned);
        var picked = new List<OwnedMethod>();
        var length = 0;
        foreach (var method in ordered)
        {
            if (picked.Count == PromptMethods) break;
            if (picked.Count > 0 && length + method.Method.Code.Length > PromptCharacters) continue;
            picked.Add(method);
            length += method.Method.Code.Length;
        }
        return picked;
    }

    private static string[] Words(string text) => Plain(text).Split(' ', StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal).ToArray();

    /// <summary>Chữ thường, bỏ dấu tiếng Việt.</summary>
    public static string Plain(string text)
    {
        var builder = new StringBuilder();
        foreach (var c in (text ?? "").Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }
}
