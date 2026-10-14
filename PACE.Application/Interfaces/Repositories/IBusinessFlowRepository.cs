using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Interfaces.Repositories;

public interface IBusinessFlowRepository : IGenericRepository<BusinessFlow>
{
    Task<List<BusinessFlowSummary>> GetSummariesByBusinessAsync(Guid businessId, CancellationToken ct = default);
    Task<BusinessFlowSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default);
    Task<string?> GetMermaidGraphAsync(Guid id, CancellationToken ct = default);
}
