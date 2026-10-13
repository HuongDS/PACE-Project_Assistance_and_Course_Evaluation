using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Common.ReadModels;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class AnalysisRunRepository(AppDbContext context) : GenericRepository<AnalysisRun>(context), IAnalysisRunRepository
{
    public Task<PagedResult<AnalysisRunSummary>> SearchSummariesAsync(string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking();
        var keyword = NormalizeSearch(search);
        if (keyword is not null)
            query = query.Where(r => (r.RepoName != null && r.RepoName.ToLower().Contains(keyword))
                                     || (r.RepoUrl != null && r.RepoUrl.ToLower().Contains(keyword))
                                     || r.Users.Any(u => u.Email.ToLower().Contains(keyword)));

        return ToPagedAsync(ToSummary(query.OrderByDescending(r => r.CreatedAt)), page, pageSize, ct);
    }

    public Task<AnalysisRunSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default)
        => ToSummary(DbSet.AsNoTracking().Where(r => r.Id == id)).FirstOrDefaultAsync(ct);

    public Task<AnalysisRun?> GetByRepoUrlAsync(string repoUrl, CancellationToken ct = default)
    {
        var normalized = repoUrl.Trim().ToLower();
        return DbSet.FirstOrDefaultAsync(r => r.RepoUrl != null && r.RepoUrl.ToLower() == normalized, ct);
    }

    public Task<bool> RepoUrlExistsAsync(string repoUrl, Guid? exceptId = null, CancellationToken ct = default)
    {
        var normalized = repoUrl.Trim().ToLower();
        return DbSet.AnyAsync(r => r.RepoUrl != null && r.RepoUrl.ToLower() == normalized && r.Id != exceptId, ct);
    }

    public Task<List<AnalysisRun>> GetAllWithRepoUrlAsync(CancellationToken ct = default)
        => DbSet.Where(r => r.RepoUrl != null).ToListAsync(ct);

    public Task<bool> HasQuestionsAsync(Guid analysisRunId, CancellationToken ct = default)
        => Context.GeneratedQuestions.AnyAsync(q => q.Business.AnalysisRunId == analysisRunId, ct);

    private static IQueryable<AnalysisRunSummary> ToSummary(IQueryable<AnalysisRun> query)
        => query.Select(r => new AnalysisRunSummary(
            r.Id, r.RepoUrl, r.RepoName, r.CreatedAt,
            r.Users.Count(u => u.Role == UserRole.Student),
            r.Businesses.Count,
            r.MethodSources.Count));
}
