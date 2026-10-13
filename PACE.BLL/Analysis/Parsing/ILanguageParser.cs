using PACE.BLL.Analysis.Models;

namespace PACE.BLL.Analysis.Parsing;

public interface ILanguageParser
{
    string Name { get; }

    bool CanParse(string relativePath);

    ParseResult Parse(IReadOnlyList<SourceFile> files, CancellationToken ct);
}
