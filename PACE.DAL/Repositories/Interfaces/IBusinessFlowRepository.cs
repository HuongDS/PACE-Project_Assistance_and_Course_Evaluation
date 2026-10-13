using PACE.DAL.Models;
using PACE.DAL.Repositories.ReadModels;

namespace PACE.DAL.Repositories.Interfaces;

public interface IBusinessFlowRepository : IGenericRepository<BusinessFlow>
{
    Task<List<BusinessFlowSummary>> GetSummariesByBusinessAsync(Guid businessId, CancellationToken ct = default);
    Task<BusinessFlowSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default);
    Task<string?> GetMermaidGraphAsync(Guid id, CancellationToken ct = default);
}
