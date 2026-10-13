using Microsoft.EntityFrameworkCore;
using PACE.DAL.Data;
using PACE.DAL.Models;
using PACE.DAL.Repositories.Interfaces;
using PACE.DAL.Repositories.ReadModels;

namespace PACE.DAL.Repositories.Implementations;

public class MethodSourceRepository(AppDbContext context) : GenericRepository<MethodSource>(context), IMethodSourceRepository
{
    public Task<PagedResult<MethodSourceSummary>> SearchSummariesAsync(
        Guid? analysisRunId, Guid? businessId, ProgrammingLanguage? language, string? search,
        int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking();
        if (analysisRunId.HasValue) query = query.Where(m => m.AnalysisRunId == analysisRunId);
        if (businessId.HasValue) query = query.Where(m => m.BusinessMethods.Any(bm => bm.BusinessId == businessId));
        if (language.HasValue) query = query.Where(m => m.Language == language);

        var keyword = NormalizeSearch(search);
        if (keyword is not null)
            query = query.Where(m => m.ClassName.ToLower().Contains(keyword)
                                     || m.MethodName.ToLower().Contains(keyword)
                                     || (m.FilePath != null && m.FilePath.ToLower().Contains(keyword)));

        var summaries = query
            .OrderBy(m => m.ClassName).ThenBy(m => m.MethodName)
            .Select(m => new MethodSourceSummary(
                m.Id, m.AnalysisRunId, m.ClassName, m.MethodName, m.FilePath, m.Language, m.NodeType, m.Parameters));

        return ToPagedAsync(summaries, page, pageSize, ct);
    }

    public Task<MethodSource?> GetWithBusinessesAsync(Guid id, CancellationToken ct = default)
        => DbSet.AsNoTracking().Include(m => m.BusinessMethods).FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<int> DeleteByRepoAsync(Guid analysisRunId, CancellationToken ct = default)
        => DbSet.Where(m => m.AnalysisRunId == analysisRunId).ExecuteDeleteAsync(ct);
}
