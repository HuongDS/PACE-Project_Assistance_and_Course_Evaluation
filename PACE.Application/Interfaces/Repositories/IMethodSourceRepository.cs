using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Interfaces.Repositories;

public interface IMethodSourceRepository : IGenericRepository<MethodSource>
{
    Task<PagedResult<MethodSourceSummary>> SearchSummariesAsync(
        Guid? analysisRunId, Guid? businessId, ProgrammingLanguage? language, string? search,
        int page, int pageSize, CancellationToken ct = default);
    Task<MethodSource?> GetWithBusinessesAsync(Guid id, CancellationToken ct = default);
    Task<int> DeleteByRepoAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<List<MethodCode>> GetCodeByBusinessAsync(Guid businessId, CancellationToken ct = default);
}
