using PACE.DAL.Models;
using PACE.DAL.Repositories.ReadModels;

namespace PACE.DAL.Repositories.Interfaces;

public interface IDataFlowEdgeRepository : IGenericRepository<DataFlowEdge>
{
    Task<List<DataFlowEdgeDetail>> GetDetailsByFlowAsync(Guid flowId, CancellationToken ct = default);
}
