using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PACE.Application.Analysis;
using PACE.Application.Analysis.Abstractions;
using PACE.Application.Analysis.Models;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.ReadModels;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.Application.Oral;

/// <summary>Một tài khoản git xuất hiện trong mã nguồn. <see cref="StudentId"/> là sinh viên được nhận ra (hoặc giảng viên chọn).</summary>
public record SnapshotAuthor(string Key, string Name, string Email, int Lines, Guid? StudentId, string? MatchedBy, string? Login = null);

/// <summary>Một commit đã viết ra ít nhất một dòng của hàm.</summary>
public record SnapshotCommit(string Sha, string Message, DateTime? At, string AuthorKey);

/// <summary>
/// Một hàm tại commit đã chụp. <paramref name="LineOwners"/> ứng với từng dòng của <paramref name="Code"/>:
/// vị trí của tác giả trong <paramref name="LineAuthors"/>, hoặc -1 nếu dòng chỉ có ngoặc, chú thích hay để trống.
/// </summary>
public record SnapshotMethod(string Key, string File, string Name, int StartLine, string Code,
    List<string> LineAuthors, List<int> LineOwners, List<SnapshotCommit> Commits);

public record CodeSnapshot(List<SnapshotAuthor> Authors, List<SnapshotMethod> Methods, List<string> Warnings);

/// <summary>Một sinh viên của phiên, kèm tài khoản git bạn ấy đã khai (nếu có).</summary>
public record RosterStudent(Guid Id, string Code, string FullName, string Email, string? GitHub = null, IReadOnlyList<string>? GitEmails = null);

public record ReadSnapshot(GitRevision Revision, CodeSnapshot Snapshot, ResearchContext Context);

/// <summary>Hàm mà một sinh viên thật sự viết: những dòng nào (số dòng trong file) và chiếm bao nhiêu phần.</summary>
public record OwnedMethod(SnapshotMethod Method, List<int> OwnLines, double Share, int Score, List<SnapshotCommit> OwnCommits);

public interface ICodeSnapshotReader
{
    /// <param name="templates">Template nghiệp vụ đã chốt cho repo; bỏ trống thì đọc trong repo hoặc tự tạo như khi phân tích.</param>
    Task<ReadSnapshot> ReadAsync(string repoUrl, DateTime? beforeUtc, IReadOnlyList<BusinessTemplate>? templates = null, CancellationToken ct = default);
}

/// <summary>Chụp mã nguồn tại mốc hạn và ghi ai viết từng dòng. Không ghi gì vào dữ liệu phân tích đã có.</summary>
public partial class CodeSnapshotReader(IGitCloneService git, IGitHistoryService history, IRepositoryScanner scanner, IEnumerable<ILanguageParser> parsers,
    IGitHubDirectory? github = null, IOptions<AnalysisOptions>? analysis = null, ITemplateGenerationService? generator = null) : ICodeSnapshotReader
{
    /// <summary>Chỉ hỏi GitHub về những người viết nhiều nhất, để không tốn hạn mức.</summary>
    private const int MaxLookups = 15;
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

    public async Task<ReadSnapshot> ReadAsync(string repoUrl, DateTime? beforeUtc, IReadOnlyList<BusinessTemplate>? templates = null, CancellationToken ct = default)
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
        var screens = scan.FrontendEntries().ToList();
        parsed.Methods.AddRange(screens);

        var warnings = new List<string>(scan.Warnings);
        // Nghiệp vụ, luồng gọi và sơ đồ dựng đúng bằng bộ phân tích của repo.
        var (context, inBusiness) = await BuildContext(repoUrl, clone, parsed, templates, warnings, ct);
        var duplicate = screens.Where(m => covered.Contains(m.FilePath)).Select(m => m.Id).ToHashSet();
        var candidates = parsed.Methods
            .Where(m => inBusiness.Contains(m.Id)
                || (!duplicate.Contains(m.Id) && !m.IsAbstract && !IsGenerated(m.FilePath) && LineCount(m.SourceCode) >= MinLines))
            .ToList();
        if (candidates.Count == 0)
            throw new UnprocessableException("Không tìm thấy hàm nào trong mã nguồn ở mốc hạn này.");

        // Mỗi file là một lần gọi git: chạy song song vài file một lúc để repo lớn không phải chờ lâu.
        var allFiles = candidates.GroupBy(m => m.FilePath, StringComparer.Ordinal)
            .OrderByDescending(g => g.Any(m => inBusiness.Contains(m.Id))).ThenByDescending(g => g.Sum(m => LineCount(m.SourceCode)))
            .Select(g => g.Key).ToList();
        if (allFiles.Count > MaxFiles) warnings.Add($"Repo lớn: chỉ xét {MaxFiles} file nhiều mã nhất trong {allFiles.Count} file.");
        var blame = new ConcurrentDictionary<string, IReadOnlyList<GitLineAuthor>>(StringComparer.Ordinal);
        await Parallel.ForEachAsync(allFiles.Take(MaxFiles), new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (path, token) => blame[path] = await history.BlameAsync(clone, path, token));

        var methods = new List<SnapshotMethod>();
        var authors = new Dictionary<string, (string Name, string Email, int Lines)>(StringComparer.Ordinal);
        var perCommit = new Dictionary<string, (string Author, int Lines)>(StringComparer.Ordinal);
        var counted = new HashSet<(string, int)>(); // hàm lồng nhau không được đếm một dòng hai lần
        var sample = new Dictionary<string, string>(StringComparer.Ordinal); // một commit của mỗi tác giả, để hỏi GitHub đó là ai
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
                if (line.Sha.Length > 0) sample.TryAdd(key, line.Sha);
                if (!counted.Add((method.FilePath, at))) continue;
                var seen = authors.GetValueOrDefault(key, (Name: line.Name, Email: line.Email, Lines: 0));
                authors[key] = (seen.Name, seen.Email, seen.Lines + 1);
                if (line.Sha.Length > 0) perCommit[line.Sha] = (line.Name, perCommit.GetValueOrDefault(line.Sha).Lines + 1);
            }
            if (lineAuthors.Count == 0) continue;
            methods.Add(new SnapshotMethod(Key(method), method.FilePath, method.Label, method.StartLine,
                string.Join('\n', code), lineAuthors, owners, commits.Values.OrderByDescending(c => c.At).Take(12).ToList()));
        }

        // Giữ lại phần nhiều nhất của từng người, để người viết hàm ngắn không bị người viết hàm dài chiếm hết chỗ.
        var keep = new HashSet<string>(StringComparer.Ordinal);
        foreach (var author in authors.Keys)
            foreach (var method in methods
                .Select(m => (m.Key, Own: Own(m, author)))
                .Where(m => m.Own > 0).OrderByDescending(m => m.Own).Take(MaxMethodsPerAuthor))
                keep.Add(method.Key);
        var flowKeys = context.Methods.Select(m => m.Key).ToHashSet(StringComparer.Ordinal);
        methods = methods.Where(m => keep.Contains(m.Key) || flowKeys.Contains(m.Key)).ToList();

        var total = perCommit.Values.Sum(c => c.Lines);
        var biggest = perCommit.OrderByDescending(c => c.Value.Lines).FirstOrDefault();
        if (total >= 200 && biggest.Value.Lines > total * BulkShare)
            warnings.Insert(0, $"Một commit ({biggest.Key[..Math.Min(8, biggest.Key.Length)]}, {biggest.Value.Author}) chiếm {biggest.Value.Lines * 100 / total}% số dòng mã. "
                + "Mã có thể được đẩy một lần hoặc gộp lịch sử, nên hãy kiểm lại ai thật sự viết.");

        var asked = authors.OrderByDescending(a => a.Value.Lines).Select(a => sample.GetValueOrDefault(a.Key)).OfType<string>().Take(MaxLookups).ToList();
        var logins = github is null ? null : await github.CommitLoginsAsync(repoUrl, asked, ct);
        var list = authors.OrderByDescending(a => a.Value.Lines)
            .Select(a => new SnapshotAuthor(a.Key, a.Value.Name, a.Value.Email, a.Value.Lines, null, null,
                logins != null && sample.TryGetValue(a.Key, out var sha) ? logins.GetValueOrDefault(sha) : null)).ToList();
        return new ReadSnapshot(revision, new CodeSnapshot(list, methods, warnings), context);
    }

    private static string Key(ParsedMethod method) => $"{method.FilePath}:{method.StartLine}:{method.Label}";

    private async Task<(ResearchContext Context, HashSet<Guid> InBusiness)> BuildContext(string repoUrl, IClonedRepository clone, ParseResult parsed,
        IReadOnlyList<BusinessTemplate>? stored, List<string> warnings, CancellationToken ct)
    {
        var templates = stored?.ToList();
        try
        {
            // Cùng thứ tự ưu tiên như khi phân tích repo: template đã chốt, file trong repo, rồi tự tạo.
            if (templates is null && await scanner.ReadBusinessTemplateAsync([clone.WorkingPath, clone.RootPath], ct) is { } json)
                templates = BusinessTemplateLoader.Parse(json);
            if (templates is null && generator != null)
                templates = (await generator.GenerateAsync(new GenerateTemplateRequest { RepoUrl = repoUrl, UseAi = true }, ct)).Businesses.ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add($"Chưa dựng được danh sách nghiệp vụ: {ex.Message}");
        }
        if (templates is not { Count: > 0 })
        {
            warnings.Add("Repo chưa có nghiệp vụ nào nên câu hỏi chỉ dựa trên mã thô. Hãy phân tích repo và chốt template nghiệp vụ.");
            return (new ResearchContext([], []), []);
        }

        var model = BusinessModelBuilder.Build(Guid.Empty, templates, parsed, analysis?.Value ?? new AnalysisOptions(), warnings);
        var keys = parsed.Methods.ToDictionary(m => m.Id, Key);
        var used = model.BusinessMethods.Select(m => m.MethodSourceId).ToHashSet();
        var businesses = model.Businesses.Select(business => new ContextBusiness(business.BusinessName,
            model.Flows.Where(f => f.BusinessId == business.Id).Select(f => new FlowQuestionContext(f.Id, f.Name, f.EntryMethodId, f.MermaidGraph,
                model.Edges.Where(e => e.FlowId == f.Id).OrderBy(e => e.StepOrder).Select(e => new FlowEdgeRef(e.StepOrder, e.FromMethodId, e.ToMethodId)).ToList())).ToList(),
            model.BusinessMethods.Where(m => m.BusinessId == business.Id).Select(m => m.MethodSourceId).ToList())).ToList();
        foreach (var flow in businesses.SelectMany(b => b.Flows))
        {
            used.Add(flow.EntryMethodId);
            foreach (var edge in flow.Edges) { used.Add(edge.FromMethodId); used.Add(edge.ToMethodId); }
        }
        var methods = model.Methods.Where(m => used.Contains(m.Id))
            .Select(m => new ContextMethod(m.Id, keys[m.Id], m.ClassName, m.MethodName, m.SourceCode)).ToList();
        return (new ResearchContext(businesses, methods), used);
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

    [GeneratedRegex(@"\b(if|for|foreach|while|switch|case|catch|throw)\b")]
    private static partial Regex Branches();

    public static List<SnapshotAuthor> Match(IEnumerable<SnapshotAuthor> authors, IReadOnlyList<RosterStudent> roster)
        => authors.Select(author =>
        {
            var email = author.Email.Trim().ToLowerInvariant();
            var local = email.Split('@')[0];
            var name = Plain(author.Name);
            // Tên tài khoản nằm trong email ẩn danh của GitHub: "123+ten@users.noreply.github.com".
            var hidden = email.EndsWith("@users.noreply.github.com", StringComparison.Ordinal) ? local.Split('+')[^1] : null;
            var login = author.Login?.Trim().ToLowerInvariant();
            var written = author.Name.Trim().ToLowerInvariant();
            // Tài khoản sinh viên đã khai được tin trước mọi phỏng đoán.
            var rules = new (string Reason, Func<RosterStudent, bool> Fits)[]
            {
                ("email đã khai", s => s.GitEmails?.Any(e => e.Equals(email, StringComparison.OrdinalIgnoreCase)) == true),
                ("tài khoản GitHub đã khai", s => s.GitHub != null && (Same(s.GitHub, login) || Same(s.GitHub, hidden))),
                ("tên trùng tài khoản GitHub đã khai", s => s.GitHub != null && (Same(s.GitHub, written) || Same(s.GitHub, local))),
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

    private static bool Same(string declared, string? seen) => declared.Equals(seen, StringComparison.OrdinalIgnoreCase);

    /// <summary>Gán lại theo thông tin mới nhất của sinh viên; chỗ giảng viên đã chọn tay thì giữ nguyên.</summary>
    public static CodeSnapshot Apply(CodeSnapshot snapshot, IReadOnlyList<RosterStudent> roster) => snapshot with
    {
        Authors = snapshot.Authors.Select(a => a.MatchedBy == Manual ? a : Match([a], roster)[0]).ToList()
    };

    public const string Manual = "giảng viên chọn";

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
