using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Common;
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

    public async Task<AnalysisRun?> GetByRepoUrlAsync(string repoUrl, CancellationToken ct = default)
    {
        var normalized = RepoUrlHelper.Normalize(repoUrl).ToLowerInvariant();
        var key = RepoUrlHelper.SourceKey(repoUrl);
        var candidates = await DbSet.Where(r => r.RepoUrl != null && r.RepoUrl.ToLower() == normalized).ToListAsync(ct);
        return candidates.FirstOrDefault(r => RepoUrlHelper.SourceKey(r.RepoUrl!) == key);
    }

    public async Task<bool> RepoUrlExistsAsync(string repoUrl, Guid? exceptId = null, CancellationToken ct = default)
    {
        var normalized = RepoUrlHelper.Normalize(repoUrl).ToLowerInvariant();
        var key = RepoUrlHelper.SourceKey(repoUrl);
        var candidates = await DbSet.AsNoTracking().Where(r => r.RepoUrl != null && r.RepoUrl.ToLower() == normalized && r.Id != exceptId)
            .Select(r => r.RepoUrl!).ToListAsync(ct);
        return candidates.Any(url => RepoUrlHelper.SourceKey(url) == key);
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
