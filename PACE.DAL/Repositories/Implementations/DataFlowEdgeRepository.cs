using Microsoft.EntityFrameworkCore;
using PACE.DAL.Data;
using PACE.DAL.Models;
using PACE.DAL.Repositories.Interfaces;
using PACE.DAL.Repositories.ReadModels;

namespace PACE.DAL.Repositories.Implementations;

public class DataFlowEdgeRepository(AppDbContext context) : GenericRepository<DataFlowEdge>(context), IDataFlowEdgeRepository
{
    public Task<List<DataFlowEdgeDetail>> GetDetailsByFlowAsync(Guid flowId, CancellationToken ct = default)
        => DbSet.AsNoTracking()
            .Where(e => e.FlowId == flowId)
            .OrderBy(e => e.StepOrder)
            .Select(e => new DataFlowEdgeDetail(
                e.Id, e.StepOrder,
                e.FromMethodId, e.FromMethod.ClassName + "." + e.FromMethod.MethodName,
                e.ToMethodId, e.ToMethod.ClassName + "." + e.ToMethod.MethodName,
                e.EdgeType, e.ConditionContext, e.DataLabel))
            .ToListAsync(ct);
}
