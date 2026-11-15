namespace PACE.Application.Analysis.Models;

public enum SourceKind
{
    CSharp,
    Java,
    JavaScriptTypeScript
}

public sealed class RepositoryScan
{
    public List<string> Stacks { get; } = [];
    public List<SourceFile> CSharpFiles { get; } = [];
    public List<SourceFile> JavaFiles { get; } = [];
    public List<SourceFile> JsTsFiles { get; } = [];
    public List<SourceFile> FrontendFiles { get; } = [];
    public List<string> Warnings { get; } = [];

    /// <summary>Số dòng log tối đa giữ lại trong bộ nhớ; bộ đếm vẫn đếm đủ.</summary>
    public const int MaxLogEntries = 5000;

    public List<ScanFileEntry> FileLog { get; } = [];
    public List<RedactionFinding> Redactions { get; } = [];
    public int AcceptedCount { get; private set; }
    public int IgnoredCount { get; private set; }
    public int FailedCount { get; private set; }

    public void Log(string relativePath, ScanFileStatus status, string reason)
    {
        switch (status)
        {
            case ScanFileStatus.Accepted: AcceptedCount++; break;
            case ScanFileStatus.Ignored: IgnoredCount++; break;
            default: FailedCount++; break;
        }
        if (FileLog.Count < MaxLogEntries) FileLog.Add(new ScanFileEntry(relativePath, status, reason));
    }

    /// <summary>Các dòng cảnh báo tóm tắt để trả cho người dùng, không nêu giá trị bí mật.</summary>
    public IEnumerable<string> RedactionWarnings()
    {
        if (Redactions.Count > 0)
            yield return $"Đã che {Redactions.Count} thông tin nhạy cảm trong {Redactions.Select(r => r.RelativePath).Distinct().Count()} file trước khi lưu và gửi sang AI.";
        if (FailedCount > 0)
            yield return $"Có {FailedCount} file không đọc hoặc không quét được thông tin nhạy cảm nên đã bị bỏ qua.";
    }

    public int TotalFiles => CSharpFiles.Count + JavaFiles.Count + JsTsFiles.Count + FrontendFiles.Count;

    public IEnumerable<ParsedMethod> FrontendEntries() => FrontendFiles.Select(file => new ParsedMethod
    {
        ClassName = FrontendClassPrefix + file.RelativePath,
        MethodName = "Screen",
        FilePath = file.RelativePath,
        Language = Path.GetExtension(file.RelativePath).ToLowerInvariant() switch
        {
            ".ts" or ".tsx" => PACE.Domain.Enums.ProgrammingLanguage.TypeScript,
            ".js" or ".jsx" or ".mjs" or ".cjs" => PACE.Domain.Enums.ProgrammingLanguage.JavaScript,
            _ => PACE.Domain.Enums.ProgrammingLanguage.Other
        },
        SourceCode = file.Content,
        StartLine = 1
    });

    public const string FrontendClassPrefix = "Frontend:";

    public IReadOnlyList<SourceFile> FilesFor(SourceKind kind) => kind switch
    {
        SourceKind.CSharp => CSharpFiles,
        SourceKind.Java => JavaFiles,
        _ => JsTsFiles
    };
}
