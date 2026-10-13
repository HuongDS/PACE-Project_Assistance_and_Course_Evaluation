using PACE.DAL.Models;
using PACE.DAL.Repositories.ReadModels;

namespace PACE.DAL.Repositories.Interfaces;

public interface IAnalysisRunRepository : IGenericRepository<AnalysisRun>
{
    Task<PagedResult<AnalysisRunSummary>> SearchSummariesAsync(string? search, int page, int pageSize, CancellationToken ct = default);
    Task<AnalysisRunSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default);
    Task<AnalysisRun?> GetByRepoUrlAsync(string repoUrl, CancellationToken ct = default);
    Task<bool> RepoUrlExistsAsync(string repoUrl, Guid? exceptId = null, CancellationToken ct = default);
    Task<List<AnalysisRun>> GetAllWithRepoUrlAsync(CancellationToken ct = default);
    Task<bool> HasQuestionsAsync(Guid analysisRunId, CancellationToken ct = default);
}
