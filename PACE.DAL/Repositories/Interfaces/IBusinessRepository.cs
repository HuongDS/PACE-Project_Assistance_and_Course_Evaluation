using PACE.DAL.Models;
using PACE.DAL.Repositories.ReadModels;

namespace PACE.DAL.Repositories.Interfaces;

public interface IBusinessRepository : IGenericRepository<Business>
{
    Task<List<BusinessSummary>> GetSummariesByRepoAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<BusinessSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default);
    Task<bool> NameExistsAsync(Guid analysisRunId, string name, Guid? exceptId = null, CancellationToken ct = default);
    Task<int> DeleteByRepoAsync(Guid analysisRunId, CancellationToken ct = default);
}
