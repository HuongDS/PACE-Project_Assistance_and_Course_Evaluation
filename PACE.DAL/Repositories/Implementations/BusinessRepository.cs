using Microsoft.EntityFrameworkCore;
using PACE.DAL.Data;
using PACE.DAL.Models;
using PACE.DAL.Repositories.Interfaces;
using PACE.DAL.Repositories.ReadModels;

namespace PACE.DAL.Repositories.Implementations;

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

    private static IQueryable<BusinessSummary> ToSummary(IQueryable<Business> query)
        => query.Select(b => new BusinessSummary(b.Id, b.AnalysisRunId, b.BusinessName, b.BusinessMethods.Count, b.Flows.Count));
}
