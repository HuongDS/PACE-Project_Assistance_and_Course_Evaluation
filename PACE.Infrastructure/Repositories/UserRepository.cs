using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class UserRepository(AppDbContext context) : GenericRepository<User>(context), IUserRepository
{
    public async Task<User?> GetByStudentCodeAsync(string studentCode, CancellationToken ct = default)
    {
        var normalized = studentCode.Trim().ToUpperInvariant();
        if (normalized.Length == 0) return null;
        var matches = await DbSet.AsNoTracking()
            .Include(u => u.AnalysisRun)
            .Where(u => u.StudentCode != null && u.StudentCode.Trim().ToUpper() == normalized)
            .Take(2)
            .ToListAsync(ct);
        // Case/whitespace variants must not select an arbitrary student's account.
        return matches.Count == 1 ? matches[0] : null;
    }

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
                                     || (u.StudentCode != null && u.StudentCode.ToLower().Contains(keyword))
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

    public async Task<List<PACE.Application.Dtos.StudentMemberDto>> GetMembersByRepoAsync(Guid analysisRunId, CancellationToken ct = default)
    {
        var result = new Dictionary<string, PACE.Application.Dtos.StudentMemberDto>(StringComparer.OrdinalIgnoreCase);

        // 1. Thành viên trực tiếp từ User.AnalysisRunId
        var directUsers = await DbSet.AsNoTracking()
            .Where(u => u.AnalysisRunId == analysisRunId)
            .OrderBy(u => u.StudentCode ?? u.Email)
            .ToListAsync(ct);

        foreach (var u in directUsers)
        {
            var key = u.Email.ToLowerInvariant();
            result[key] = new PACE.Application.Dtos.StudentMemberDto(u.Id, u.StudentCode, u.FullName, u.Email, "Member");
        }

        // 2. Thành viên qua Group và GroupMember của repo
        var groupMembers = await Context.GroupMembers.AsNoTracking()
            .Where(m => m.Group.AnalysisRuns.Any(r => r.Id == analysisRunId))
            .Include(m => m.StudentProfile)
                .ThenInclude(p => p.User)
            .ToListAsync(ct);

        foreach (var gm in groupMembers)
        {
            var user = gm.StudentProfile?.User;
            var email = user?.Email ?? (gm.StudentCode.ToLowerInvariant() + "@fpt.edu.vn");
            var key = email.ToLowerInvariant();
            var id = user?.Id ?? Guid.NewGuid();
            var fullName = user?.FullName ?? ("Sinh viên " + gm.StudentCode);
            var role = !string.IsNullOrEmpty(gm.Role) ? gm.Role : "Member";

            result[key] = new PACE.Application.Dtos.StudentMemberDto(id, gm.StudentCode, fullName, email, role);
        }

        return result.Values.OrderBy(m => m.Role == "Leader" ? 0 : 1).ThenBy(m => m.StudentCode).ToList();
    }
}
