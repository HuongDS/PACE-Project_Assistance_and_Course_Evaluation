using PACE.Application.Analysis.Models;

namespace PACE.Application.Analysis.Abstractions;

public interface IRepositoryScanner
{
    RepositoryScan Scan(string root, CancellationToken ct);

    Task<string?> ReadBusinessTemplateAsync(IEnumerable<string> roots, CancellationToken ct);
}
