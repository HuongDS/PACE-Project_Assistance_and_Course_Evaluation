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
    public Task<PagedResult<AnalysisRunSummary>> SearchSummariesAsync(
        string? search,
        int page,
        int pageSize,
        Guid? classId = null,
        IReadOnlyList<Guid>? allowedClassIds = null,
        Guid? singleRepoId = null,
        CancellationToken ct = default)
    {
        var query = DbSet.AsNoTracking().Where(r => !r.IsArchived);

        if (singleRepoId.HasValue)
        {
            query = query.Where(r => r.Id == singleRepoId.Value);
        }
        else if (classId.HasValue)
        {
            query = query.Where(r => r.Group != null && r.Group.ClassId == classId.Value);
        }
        else if (allowedClassIds != null)
        {
            query = query.Where(r => r.Group != null && allowedClassIds.Contains(r.Group.ClassId));
        }

        var keyword = NormalizeSearch(search);
        if (keyword is not null)
        {
            query = query.Where(r =>
                (r.RepoName != null && r.RepoName.ToLower().Contains(keyword)) ||
                (r.RepoUrl != null && r.RepoUrl.ToLower().Contains(keyword)) ||
                r.Users.Any(u => u.Email.ToLower().Contains(keyword) || (u.StudentCode != null && u.StudentCode.ToLower().Contains(keyword))) ||
                (r.Group != null && (
                    r.Group.Name.ToLower().Contains(keyword) ||
                    (r.Group.TopicName != null && r.Group.TopicName.ToLower().Contains(keyword))
                )));
        }

        return ToPagedAsync(ToSummary(query.OrderByDescending(r => r.CreatedAt)), page, pageSize, ct);
    }

    public async Task<AnalysisRunSummary?> GetSummaryAsync(Guid id, CancellationToken ct = default)
    {
        var summary = await ToSummary(DbSet.AsNoTracking().Where(r => r.Id == id)).FirstOrDefaultAsync(ct);
        if (summary == null) return null;

        // Nếu thiếu Group/Class/Lecturer, thử tra cứu bổ sung qua Group, GroupMember hoặc CourseProject
        if (summary.ClassName == null || summary.LecturerName == null || summary.GroupName == null)
        {
            var grp = await Context.Groups.AsNoTracking()
                .Include(g => g.Class)
                    .ThenInclude(c => c.Course)
                        .ThenInclude(co => co.Lecturer)
                .Include(g => g.Class)
                    .ThenInclude(c => c.Semester)
                .FirstOrDefaultAsync(g => g.AnalysisRunId == id, ct);

            if (grp == null)
            {
                var userIds = await Context.Users
                    .Where(u => u.AnalysisRunId == id && u.Role == UserRole.Student)
                    .Select(u => u.Id)
                    .ToListAsync(ct);

                if (userIds.Count > 0)
                {
                    grp = await Context.GroupMembers.AsNoTracking()
                        .Where(gm => gm.StudentProfile != null && userIds.Contains(gm.StudentProfile.UserId))
                        .Select(gm => gm.Group)
                        .Include(g => g.Class)
                            .ThenInclude(c => c.Course)
                                .ThenInclude(co => co.Lecturer)
                        .Include(g => g.Class)
                            .ThenInclude(c => c.Semester)
                        .FirstOrDefaultAsync(ct);
                }
            }

            var cp = await Context.CourseProjects.AsNoTracking()
                .Include(p => p.Course)
                    .ThenInclude(c => c.Lecturer)
                .FirstOrDefaultAsync(p => p.AnalysisRunId == id, ct);

            var groupName = summary.GroupName ?? grp?.Name;
            var topicName = summary.TopicName ?? grp?.TopicName;
            var classId = summary.ClassId ?? grp?.ClassId;
            var className = summary.ClassName ?? grp?.Class?.Name;

            var lecturerName = summary.LecturerName
                ?? (!string.IsNullOrEmpty(grp?.Class?.Course?.Lecturer?.FullName) ? grp.Class.Course.Lecturer.FullName : grp?.Class?.Course?.Lecturer?.Email)
                ?? (!string.IsNullOrEmpty(cp?.Course?.Lecturer?.FullName) ? cp.Course.Lecturer.FullName : cp?.Course?.Lecturer?.Email);

            var lecturerEmail = summary.LecturerEmail
                ?? grp?.Class?.Course?.Lecturer?.Email
                ?? cp?.Course?.Lecturer?.Email;

            var courseCode = summary.CourseCode
                ?? grp?.Class?.Course?.Code
                ?? cp?.Course?.Code;

            var courseName = summary.CourseName
                ?? grp?.Class?.Course?.Name
                ?? cp?.Course?.Name;

            var semester = summary.Semester
                ?? grp?.Class?.Course?.Semester
                ?? grp?.Class?.Semester?.Name
                ?? cp?.Course?.Semester;

            summary = summary with
            {
                GroupName = groupName,
                TopicName = topicName,
                ClassId = classId,
                ClassName = className,
                LecturerName = lecturerName,
                LecturerEmail = lecturerEmail,
                CourseCode = courseCode,
                CourseName = courseName,
                Semester = semester
            };
        }

        return summary;
    }

    public async Task<AnalysisRun?> GetByRepoUrlAsync(string repoUrl, CancellationToken ct = default)
    {
        var normalized = RepoUrlHelper.Normalize(repoUrl).ToLowerInvariant();
        var key = RepoUrlHelper.SourceKey(repoUrl);
        var candidates = await DbSet.Where(r => !r.IsArchived && r.RepoUrl != null && r.RepoUrl.ToLower() == normalized).ToListAsync(ct);
        return candidates.FirstOrDefault(r => RepoUrlHelper.SourceKey(r.RepoUrl!) == key);
    }

    public async Task<bool> RepoUrlExistsAsync(string repoUrl, Guid? exceptId = null, CancellationToken ct = default)
    {
        var normalized = RepoUrlHelper.Normalize(repoUrl).ToLowerInvariant();
        var key = RepoUrlHelper.SourceKey(repoUrl);
        var candidates = await DbSet.AsNoTracking().Where(r => !r.IsArchived && r.RepoUrl != null && r.RepoUrl.ToLower() == normalized && r.Id != exceptId)
            .Select(r => r.RepoUrl!).ToListAsync(ct);
        return candidates.Any(url => RepoUrlHelper.SourceKey(url) == key);
    }

    public Task<List<AnalysisRun>> GetAllWithRepoUrlAsync(CancellationToken ct = default)
        => DbSet.Where(r => !r.IsArchived && r.RepoUrl != null).ToListAsync(ct);

    // Must run in the same transaction as insertion of the replacement analysis.
    public async Task ArchiveCurrentAsync(Guid analysisRunId, CancellationToken ct = default)
    {
        if (!await Context.Businesses.AnyAsync(b => b.AnalysisRunId == analysisRunId, ct)
            && !await Context.MethodSources.AnyAsync(m => m.AnalysisRunId == analysisRunId, ct)) return;
        var current = await DbSet.AsNoTracking().SingleAsync(r => r.Id == analysisRunId, ct);
        var archiveId = Guid.NewGuid();
        var name = (current.RepoName ?? "Repository");
        if (name.Length > 210) name = name[..210];
        DbSet.Add(new AnalysisRun { Id = archiveId, IsArchived = true,
            RepoName = name + " (Lịch sử " + DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm") + ")",
            RepoUrl = current.AnalyzedRepoUrl ?? current.RepoUrl, AnalyzedRepoUrl = current.AnalyzedRepoUrl ?? current.RepoUrl,
            AnalyzedAt = current.AnalyzedAt, AnalyzedCommitSha = current.AnalyzedCommitSha,
            BusinessTemplateJson = current.BusinessTemplateJson, TemplateSource = current.TemplateSource,
            TemplateUpdatedAt = current.TemplateUpdatedAt });
        await Context.SaveChangesAsync(ct);
        await Context.Businesses.Where(b => b.AnalysisRunId == analysisRunId)
            .ExecuteUpdateAsync(set => set.SetProperty(b => b.AnalysisRunId, archiveId), ct);
        await Context.MethodSources.Where(m => m.AnalysisRunId == analysisRunId)
            .ExecuteUpdateAsync(set => set.SetProperty(m => m.AnalysisRunId, archiveId), ct);
        // Empty drafts can continue with the new questions. Work already started stays with its source snapshot.
        await Context.StudentSubmissions.Where(s => s.AnalysisRunId == analysisRunId && (s.SubmittedAt != null || s.Answers.Any()))
            .ExecuteUpdateAsync(set => set.SetProperty(s => s.AnalysisRunId, archiveId), ct);
    }

    public Task<List<string>> GetAllRepoNamesAsync(CancellationToken ct = default)
        => DbSet.AsNoTracking().Where(r => r.RepoName != null).Select(r => r.RepoName!).ToListAsync(ct);

    public Task<bool> HasQuestionsAsync(Guid analysisRunId, CancellationToken ct = default)
        => Context.GeneratedQuestions.AnyAsync(q => q.Business.AnalysisRunId == analysisRunId, ct);

    private static IQueryable<AnalysisRunSummary> ToSummary(IQueryable<AnalysisRun> query)
        => query.Select(r => new AnalysisRunSummary(
            r.Id, r.RepoUrl, r.RepoName, r.CreatedAt,
            r.Users.Count(u => u.Role == UserRole.Student),
            r.Businesses.Count,
            r.MethodSources.Count,
            r.Group != null ? r.Group.Name : null,
            r.Group != null ? r.Group.TopicName : null,
            r.Group != null ? (Guid?)r.Group.ClassId : null,
            r.Group != null && r.Group.Class != null ? r.Group.Class.Name : null,
            r.Group != null && r.Group.Class != null && r.Group.Class.Course != null && r.Group.Class.Course.Lecturer != null
                ? (!string.IsNullOrEmpty(r.Group.Class.Course.Lecturer.FullName) ? r.Group.Class.Course.Lecturer.FullName : r.Group.Class.Course.Lecturer.Email)
                : null,
            r.Group != null && r.Group.Class != null && r.Group.Class.Course != null && r.Group.Class.Course.Lecturer != null
                ? r.Group.Class.Course.Lecturer.Email
                : null,
            r.Group != null && r.Group.Class != null && r.Group.Class.Course != null
                ? r.Group.Class.Course.Code
                : null,
            r.Group != null && r.Group.Class != null && r.Group.Class.Course != null
                ? r.Group.Class.Course.Name
                : null,
            r.Group != null && r.Group.Class != null && r.Group.Class.Course != null
                ? r.Group.Class.Course.Semester
                : (r.Group != null && r.Group.Class != null && r.Group.Class.Semester != null ? r.Group.Class.Semester.Name : null)));

    public async Task<bool> IsLecturerOfClassAsync(Guid classId, Guid lecturerUserId, CancellationToken ct = default)
    {
        var lecturerCode = await Context.LecturerProfiles
            .Where(l => l.UserId == lecturerUserId)
            .Select(l => l.LecturerCode)
            .FirstOrDefaultAsync(ct);

        return await Context.Classes
            .Where(c => c.Id == classId)
            .AnyAsync(c => c.Course.LecturerId == lecturerUserId
                        || (lecturerCode != null && (c.Schedules.Any(s => s.LecturerCode == lecturerCode)
                                                 || c.Sessions.Any(s => s.LecturerCode == lecturerCode))), ct);
    }

    public async Task<bool> IsLecturerOfRepoClassAsync(Guid repoId, Guid lecturerUserId, CancellationToken ct = default)
    {
        var lecturerCode = await Context.LecturerProfiles
            .Where(l => l.UserId == lecturerUserId)
            .Select(l => l.LecturerCode)
            .FirstOrDefaultAsync(ct);

        return await Context.Groups
            .Where(g => g.AnalysisRunId == repoId)
            .AnyAsync(g => g.Class.Course.LecturerId == lecturerUserId
                        || (lecturerCode != null && (g.Class.Schedules.Any(s => s.LecturerCode == lecturerCode)
                                                 || g.Class.Sessions.Any(s => s.LecturerCode == lecturerCode))), ct)
            || await Context.CourseProjects
                .AnyAsync(cp => cp.AnalysisRunId == repoId && cp.Course.LecturerId == lecturerUserId, ct);
    }

    public async Task<List<Guid>> GetLecturerClassIdsAsync(Guid lecturerUserId, CancellationToken ct = default)
    {
        var lecturerCode = await Context.LecturerProfiles
            .Where(l => l.UserId == lecturerUserId)
            .Select(l => l.LecturerCode)
            .FirstOrDefaultAsync(ct);

        var scheduleClassIds = !string.IsNullOrEmpty(lecturerCode)
            ? await Context.ClassSchedules
                .Where(s => s.LecturerCode == lecturerCode)
                .Select(s => s.ClassId)
                .ToListAsync(ct)
            : [];

        var sessionClassIds = !string.IsNullOrEmpty(lecturerCode)
            ? await Context.ClassSessions
                .Where(s => s.LecturerCode == lecturerCode)
                .Select(s => s.ClassId)
                .ToListAsync(ct)
            : [];

        var courseClassIds = await Context.Classes
            .Where(c => c.Course.LecturerId == lecturerUserId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        return scheduleClassIds.Concat(sessionClassIds).Concat(courseClassIds).Distinct().ToList();
    }

    public async Task<bool> IsStudentOfRepoAsync(Guid repoId, Guid studentUserId, CancellationToken ct = default)
    {
        // 1. Kiểm tra trực tiếp trên User.AnalysisRunId
        var isDirectMember = await Context.Users
            .AnyAsync(u => u.Id == studentUserId && u.AnalysisRunId == repoId, ct);
        if (isDirectMember) return true;

        // 2. Kiểm tra qua GroupMember của nhóm sở hữu repo
        var studentCode = await Context.Users
            .Where(u => u.Id == studentUserId)
            .Select(u => u.StudentCode)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(studentCode)) return false;

        return await Context.Groups
            .Where(g => g.AnalysisRunId == repoId)
            .AnyAsync(g => g.Members.Any(m => m.StudentCode == studentCode), ct);
    }

    public async Task<Guid?> GetStudentRepoIdAsync(Guid studentUserId, CancellationToken ct = default)
    {
        var directRunId = await Context.Users
            .Where(u => u.Id == studentUserId)
            .Select(u => u.AnalysisRunId)
            .FirstOrDefaultAsync(ct);
        if (directRunId.HasValue) return directRunId.Value;

        var studentCode = await Context.Users
            .Where(u => u.Id == studentUserId)
            .Select(u => u.StudentCode)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrEmpty(studentCode)) return null;

        return await Context.GroupMembers
            .Where(m => m.StudentCode == studentCode && m.Group.AnalysisRunId.HasValue)
            .Select(m => m.Group.AnalysisRunId)
            .FirstOrDefaultAsync(ct);
    }
}
