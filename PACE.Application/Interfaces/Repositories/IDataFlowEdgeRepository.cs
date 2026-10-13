using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Domain.Entities;
using PACE.Domain.Enums;

namespace PACE.Application.Interfaces.Repositories;

public interface IDataFlowEdgeRepository : IGenericRepository<DataFlowEdge>
{
    Task<List<DataFlowEdgeDetail>> GetDetailsByFlowAsync(Guid flowId, CancellationToken ct = default);
}
