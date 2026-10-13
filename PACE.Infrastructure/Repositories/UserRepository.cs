using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : GenericRepository<User>(context), IUserRepository
{
    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLower();
        return DbSet.AsNoTracking()
            .Include(u => u.AnalysisRun)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalized, ct);
    }

    public Task<User?> GetWithRepoAsync(Guid id, bool asNoTracking = true, CancellationToken ct = default)
        => Source(asNoTracking).Include(u => u.AnalysisRun).FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> EmailExistsAsync(string email, Guid? exceptId = null, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLower();
        return DbSet.AnyAsync(u => u.Email.ToLower() == normalized && u.Id != exceptId, ct);
    }

    public Task<PagedResult<User>> SearchAsync(UserRole? role, Guid? analysisRunId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        IQueryable<User> query = DbSet.AsNoTracking().Include(u => u.AnalysisRun);
        if (role.HasValue) query = query.Where(u => u.Role == role);
        if (analysisRunId.HasValue) query = query.Where(u => u.AnalysisRunId == analysisRunId);

        var keyword = NormalizeSearch(search);
        if (keyword is not null)
            query = query.Where(u => u.Email.ToLower().Contains(keyword)
                                     || (u.AnalysisRun != null && u.AnalysisRun.RepoName != null && u.AnalysisRun.RepoName.ToLower().Contains(keyword)));

        return ToPagedAsync(query.OrderBy(u => u.Role).ThenBy(u => u.Email), page, pageSize, ct);
    }

    public async Task<HashSet<string>> GetAllEmailsAsync(CancellationToken ct = default)
        => (await DbSet.AsNoTracking().Select(u => u.Email.ToLower()).ToListAsync(ct)).ToHashSet();

    public Task<List<string>> GetEmailsByRepoAsync(Guid analysisRunId, CancellationToken ct = default)
        => DbSet.AsNoTracking()
            .Where(u => u.AnalysisRunId == analysisRunId)
            .OrderBy(u => u.Email)
            .Select(u => u.Email)
            .ToListAsync(ct);
}
