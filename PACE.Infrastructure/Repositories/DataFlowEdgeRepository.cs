using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

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
