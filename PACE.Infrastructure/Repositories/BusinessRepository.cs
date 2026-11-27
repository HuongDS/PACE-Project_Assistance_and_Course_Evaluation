using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class BusinessRepository(AppDbContext context) : GenericRepository<Business>(context), IBusinessRepository
{
    public Task<List<BusinessSummary>> GetSummariesByRepoAsync(Guid analysisRunId, CancellationToken ct = default)
        => ToSummary(DbSet.AsNoTracking()
                .Where(b => b.AnalysisRunId == analysisRunId)
                .OrderBy(b => b.BusinessName))
            .ToListAsync(ct);

    public Task<BusinessSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default)
        => ToSummary(DbSet.AsNoTracking().Where(b => b.Id == id)).FirstOrDefaultAsync(ct);

    public Task<bool> NameExistsAsync(Guid analysisRunId, string name, Guid? exceptId = null, CancellationToken ct = default)
    {
        var normalized = name.Trim().ToLower();
        return DbSet.AnyAsync(b => b.AnalysisRunId == analysisRunId && b.Id != exceptId && b.BusinessName.ToLower() == normalized, ct);
    }

    public Task<int> DeleteByRepoAsync(Guid analysisRunId, CancellationToken ct = default)
        => DbSet.Where(b => b.AnalysisRunId == analysisRunId).ExecuteDeleteAsync(ct);

    public Task<List<Business>> GetWithApisByRepoAsync(Guid analysisRunId, CancellationToken ct = default)
        => DbSet.AsNoTracking()
            .Include(b => b.Apis)
            .Where(b => b.AnalysisRunId == analysisRunId)
            .OrderBy(b => b.OrderIndex)
            .ThenBy(b => b.CreatedAt)
            .ToListAsync(ct);

    public async Task SyncApisAsync(Guid businessId, string? description, IReadOnlyCollection<BusinessApi> apis, CancellationToken ct = default)
    {
        await Context.BusinessApis.Where(a => a.BusinessId == businessId).ExecuteDeleteAsync(ct);
        var now = DateTime.UtcNow;
        if (description is null)
            await DbSet.Where(b => b.Id == businessId).ExecuteUpdateAsync(s => s.SetProperty(b => b.UpdatedAt, now), ct);
        else
            await DbSet.Where(b => b.Id == businessId).ExecuteUpdateAsync(s => s
                .SetProperty(b => b.Description, description)
                .SetProperty(b => b.UpdatedAt, now), ct);
        foreach (var api in apis) api.BusinessId = businessId;
        await Context.BusinessApis.AddRangeAsync(apis, ct);
    }

    private static IQueryable<BusinessSummary> ToSummary(IQueryable<Business> query)
        => query.Select(b => new BusinessSummary(b.Id, b.AnalysisRunId, b.BusinessName, b.BusinessMethods.Count, b.Flows.Count));
}
