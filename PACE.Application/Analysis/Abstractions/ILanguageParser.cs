using PACE.Application.Analysis.Models;

namespace PACE.Application.Analysis.Abstractions;

public interface ILanguageParser
{
    string Name { get; }

    SourceKind Kind { get; }

    ParseResult Parse(IReadOnlyList<SourceFile> files, CancellationToken ct);
}
