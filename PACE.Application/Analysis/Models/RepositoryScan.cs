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
    public List<string> Warnings { get; } = [];
    public int TotalFiles => CSharpFiles.Count + JavaFiles.Count + JsTsFiles.Count;

    public IReadOnlyList<SourceFile> FilesFor(SourceKind kind) => kind switch
    {
        SourceKind.CSharp => CSharpFiles,
        SourceKind.Java => JavaFiles,
        _ => JsTsFiles
    };
}
