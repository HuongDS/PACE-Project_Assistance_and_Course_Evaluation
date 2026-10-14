using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

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
