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
