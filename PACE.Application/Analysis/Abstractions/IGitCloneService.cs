namespace PACE.Application.Analysis.Abstractions;

public interface IClonedRepository : IDisposable
{
    string RootPath { get; }
    string WorkingPath { get; }
}

public interface IGitCloneService
{
    Task<IClonedRepository> CloneAsync(string repoUrl, CancellationToken ct = default);
}
