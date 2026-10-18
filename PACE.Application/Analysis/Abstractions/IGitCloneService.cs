namespace PACE.Application.Analysis.Abstractions;

public interface IClonedRepository : IDisposable
{
    string RootPath { get; }
    string WorkingPath { get; }
    string? CommitSha => null;
}

public interface IGitCloneService
{
    Task<IClonedRepository> CloneAsync(string repoUrl, CancellationToken ct = default);
}
