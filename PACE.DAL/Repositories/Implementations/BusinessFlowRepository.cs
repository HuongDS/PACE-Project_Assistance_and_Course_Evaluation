using Microsoft.EntityFrameworkCore;
using PACE.DAL.Data;
using PACE.DAL.Models;
using PACE.DAL.Repositories.Interfaces;
using PACE.DAL.Repositories.ReadModels;

namespace PACE.DAL.Repositories.Implementations;

public class BusinessFlowRepository(AppDbContext context) : GenericRepository<BusinessFlow>(context), IBusinessFlowRepository
{
    public Task<List<BusinessFlowSummary>> GetSummariesByBusinessAsync(Guid businessId, CancellationToken ct = default)
        => ToSummary(DbSet.AsNoTracking()
                .Where(f => f.BusinessId == businessId)
                .OrderBy(f => f.Name))
            .ToListAsync(ct);

    public Task<BusinessFlowSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default)
        => ToSummary(DbSet.AsNoTracking().Where(f => f.Id == id)).FirstOrDefaultAsync(ct);

    public Task<string?> GetMermaidGraphAsync(Guid id, CancellationToken ct = default)
        => DbSet.AsNoTracking().Where(f => f.Id == id).Select(f => f.MermaidGraph).FirstOrDefaultAsync(ct);

    private static IQueryable<BusinessFlowSummary> ToSummary(IQueryable<BusinessFlow> query)
        => query.Select(f => new BusinessFlowSummary(
            f.Id, f.BusinessId, f.Name, f.EntryMethodId,
            f.EntryMethod.ClassName + "." + f.EntryMethod.MethodName,
            f.Edges.Count, f.CreatedAt));
}
