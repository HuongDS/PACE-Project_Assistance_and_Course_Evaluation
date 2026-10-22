using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Interfaces.Repositories;

public interface IAnalysisRunRepository : IGenericRepository<AnalysisRun>
{
    Task<PagedResult<AnalysisRunSummary>> SearchSummariesAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<AnalysisRunSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default);
    Task<AnalysisRun?> GetByRepoUrlAsync(string repoUrl, CancellationToken ct = default);
    Task<bool> RepoUrlExistsAsync(string repoUrl, Guid? exceptId = null, CancellationToken ct = default);
    Task<List<AnalysisRun>> GetAllWithRepoUrlAsync(CancellationToken ct = default);
    Task<List<string>> GetAllRepoNamesAsync(CancellationToken ct = default);
    Task ArchiveCurrentAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<bool> HasQuestionsAsync(Guid analysisRunId, CancellationToken ct = default);
}
