using PACE.DAL.Models;
using PACE.DAL.Repositories.ReadModels;

namespace PACE.DAL.Repositories.Interfaces;

public interface IMethodSourceRepository : IGenericRepository<MethodSource>
{
    Task<PagedResult<MethodSourceSummary>> SearchSummariesAsync(
        Guid? analysisRunId, Guid? businessId, ProgrammingLanguage? language, string? search,
        int page, int pageSize, CancellationToken ct = default);
    Task<MethodSource?> GetWithBusinessesAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByRepoAsync(Guid analysisRunId, CancellationToken ct = default);
}
