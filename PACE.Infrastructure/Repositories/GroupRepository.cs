using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Interfaces.Repositories;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.Infrastructure.Repositories;

public class GroupRepository(AppDbContext context) : GenericRepository<Group>(context), IGroupRepository
{
    public async Task<PagedResult<GroupDto>> SearchGroupsAsync(GroupQuery query, CancellationToken ct = default)
    {
        var q = DbSet.AsNoTracking();

        if (query.ClassId.HasValue)
        {
            q = q.Where(g => g.ClassId == query.ClassId.Value);
        }

        var search = NormalizeSearch(query.SearchTerm);
        if (!string.IsNullOrEmpty(search))
        {
            q = q.Where(g => g.Name.ToLower().Contains(search) || (g.TopicName != null && g.TopicName.ToLower().Contains(search)));
        }

        var total = await q.CountAsync(ct);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

        var items = await q
            .OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(g => new GroupDto(
                g.Id,
                g.ClassId,
                g.Name,
                g.TopicName,
                g.CreatedAt,
                g.Members.Count))
            .ToListAsync(ct);

        return new PagedResult<GroupDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<GroupDetailDto?> GetGroupDetailAsync(Guid id, CancellationToken ct = default)
    {
        var group = await DbSet.AsNoTracking()
            .Include(g => g.Class)
            .Include(g => g.Members)
                .ThenInclude(m => m.StudentProfile)
                    .ThenInclude(sp => sp.User)
            .FirstOrDefaultAsync(g => g.Id == id, ct);

        if (group == null) return null;

        var members = group.Members
            .Select(m => new GroupMemberDto(
                m.GroupId,
                m.StudentCode,
                m.StudentProfile?.UserId,
                m.StudentProfile?.User?.FullName,
                m.StudentProfile?.User?.Email,
                m.Role,
                m.JoinedAt))
            .ToList();

        return new GroupDetailDto(
            group.Id,
            group.ClassId,
            group.Class?.Name ?? "",
            group.Name,
            group.TopicName,
            group.CreatedAt,
            members);
    }

    public async Task<List<GroupMemberDto>> GetGroupMembersAsync(Guid groupId, CancellationToken ct = default)
    {
        return await Context.GroupMembers.AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .Include(m => m.StudentProfile)
                .ThenInclude(sp => sp.User)
            .Select(m => new GroupMemberDto(
                m.GroupId,
                m.StudentCode,
                m.StudentProfile.UserId,
                m.StudentProfile.User.FullName,
                m.StudentProfile.User.Email,
                m.Role,
                m.JoinedAt))
            .ToListAsync(ct);
    }

    public async Task<GroupMember?> GetMemberAsync(Guid groupId, string studentCode, CancellationToken ct = default)
    {
        return await Context.GroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == groupId && m.StudentCode.ToLower() == studentCode.ToLower(), ct);
    }

    public async Task AddMemberAsync(GroupMember member, CancellationToken ct = default)
    {
        await Context.GroupMembers.AddAsync(member, ct);
    }

    public void RemoveMember(GroupMember member)
    {
        Context.GroupMembers.Remove(member);
    }

    // ==========================================
    // Group Member Check Methods
    // ==========================================

    public async Task<bool> IsStudentInAnyGroupInClassAsync(Guid classId, string studentCode, CancellationToken ct = default)
    {
        return await Context.GroupMembers.AsNoTracking()
            .AnyAsync(m => m.Group.ClassId == classId && m.StudentCode.ToLower() == studentCode.ToLower(), ct);
    }

    public async Task<Group?> GetGroupByStudentCodeInClassAsync(Guid classId, string studentCode, CancellationToken ct = default)
    {
        return await DbSet
            .Include(g => g.Class)
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.ClassId == classId && g.Members.Any(m => m.StudentCode.ToLower() == studentCode.ToLower()), ct);
    }

    public async Task<List<Group>> GetGroupsByStudentCodeAsync(string studentCode, CancellationToken ct = default)
    {
        return await DbSet.AsNoTracking()
            .Include(g => g.Class)
            .Include(g => g.Members)
            .Where(g => g.Members.Any(m => m.StudentCode.ToLower() == studentCode.ToLower()))
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(ct);
    }

    // ==========================================
    // Invitations
    // ==========================================

    public async Task<GroupInvitation?> GetInvitationByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await Context.GroupInvitations
            .Include(i => i.Group)
                .ThenInclude(g => g.Class)
            .Include(i => i.InvitedStudent)
                .ThenInclude(s => s.User)
            .Include(i => i.InvitedByUser)
            .FirstOrDefaultAsync(i => i.Id == id, ct);
    }

    public async Task<List<GroupInvitationDto>> GetGroupInvitationsAsync(Guid groupId, CancellationToken ct = default)
    {
        return await Context.GroupInvitations.AsNoTracking()
            .Where(i => i.GroupId == groupId)
            .Include(i => i.Group)
            .Include(i => i.InvitedStudent)
                .ThenInclude(s => s.User)
            .Include(i => i.InvitedByUser)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new GroupInvitationDto(
                i.Id,
                i.GroupId,
                i.Group.Name,
                i.InvitedStudentCode,
                i.InvitedStudent.User.FullName,
                i.InvitedStudent.User.Email,
                i.InvitedByUserId,
                i.InvitedByUser.FullName,
                i.Status.ToString(),
                i.Message,
                i.CreatedAt,
                i.RespondedAt))
            .ToListAsync(ct);
    }

    public async Task<List<GroupInvitationDto>> GetStudentInvitationsAsync(string studentCode, CancellationToken ct = default)
    {
        return await Context.GroupInvitations.AsNoTracking()
            .Where(i => i.InvitedStudentCode.ToLower() == studentCode.ToLower())
            .Include(i => i.Group)
            .Include(i => i.InvitedStudent)
                .ThenInclude(s => s.User)
            .Include(i => i.InvitedByUser)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new GroupInvitationDto(
                i.Id,
                i.GroupId,
                i.Group.Name,
                i.InvitedStudentCode,
                i.InvitedStudent.User.FullName,
                i.InvitedStudent.User.Email,
                i.InvitedByUserId,
                i.InvitedByUser.FullName,
                i.Status.ToString(),
                i.Message,
                i.CreatedAt,
                i.RespondedAt))
            .ToListAsync(ct);
    }

    public async Task AddInvitationAsync(GroupInvitation invitation, CancellationToken ct = default)
    {
        await Context.GroupInvitations.AddAsync(invitation, ct);
    }

    public async Task CancelPendingInvitationsForStudentInClassAsync(Guid classId, string studentCode, CancellationToken ct = default)
    {
        var pending = await Context.GroupInvitations
            .Where(i => i.Group.ClassId == classId &&
                        i.InvitedStudentCode.ToLower() == studentCode.ToLower() &&
                        i.Status == GroupInvitationStatus.Pending)
            .ToListAsync(ct);

        foreach (var inv in pending)
        {
            inv.Status = GroupInvitationStatus.Cancelled;
            inv.RespondedAt = DateTime.UtcNow;
        }
    }

    // ==========================================
    // Change Requests
    // ==========================================

    public async Task<GroupChangeRequest?> GetChangeRequestByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await Context.GroupChangeRequests
            .Include(r => r.Group)
                .ThenInclude(g => g.Class)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReviewedByUser)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
    }

    public async Task<List<GroupChangeRequestDto>> GetGroupChangeRequestsAsync(Guid groupId, CancellationToken ct = default)
    {
        return await Context.GroupChangeRequests.AsNoTracking()
            .Where(r => r.GroupId == groupId)
            .Include(r => r.Group)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReviewedByUser)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new GroupChangeRequestDto(
                r.Id,
                r.GroupId,
                r.Group.Name,
                r.Type.ToString(),
                r.Status.ToString(),
                r.Title,
                r.Reason,
                r.PayloadJson,
                r.FailStudentIfRemoved,
                r.CreatedByUserId,
                r.CreatedByUser.FullName,
                r.CreatedAt,
                r.ReviewedByUserId,
                r.ReviewedByUser != null ? r.ReviewedByUser.FullName : null,
                r.ReviewNotes,
                r.ReviewedAt))
            .ToListAsync(ct);
    }

    public async Task<List<GroupChangeRequestDto>> GetClassChangeRequestsAsync(Guid classId, CancellationToken ct = default)
    {
        return await Context.GroupChangeRequests.AsNoTracking()
            .Where(r => r.Group.ClassId == classId)
            .Include(r => r.Group)
            .Include(r => r.CreatedByUser)
            .Include(r => r.ReviewedByUser)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new GroupChangeRequestDto(
                r.Id,
                r.GroupId,
                r.Group.Name,
                r.Type.ToString(),
                r.Status.ToString(),
                r.Title,
                r.Reason,
                r.PayloadJson,
                r.FailStudentIfRemoved,
                r.CreatedByUserId,
                r.CreatedByUser.FullName,
                r.CreatedAt,
                r.ReviewedByUserId,
                r.ReviewedByUser != null ? r.ReviewedByUser.FullName : null,
                r.ReviewNotes,
                r.ReviewedAt))
            .ToListAsync(ct);
    }

    public async Task AddChangeRequestAsync(GroupChangeRequest request, CancellationToken ct = default)
    {
        await Context.GroupChangeRequests.AddAsync(request, ct);
    }

    // ==========================================
    // Course Topics
    // ==========================================

    public async Task<List<CourseTopicDto>> GetCourseTopicsAsync(Guid courseId, Guid? semesterId, Guid? classId, bool includeGroups, Guid? lecturerId, CancellationToken ct = default)
    {
        var query = Context.CourseTopics.AsNoTracking().Where(t => t.CourseId == courseId);
        if (semesterId != null) query = query.Where(t => t.SemesterId == null || t.SemesterId == semesterId);

        var topics = await query
            .OrderBy(t => t.Title)
            .Select(t => new
            {
                Topic = t,
                Count = t.Groups.Count(g => classId == null || g.ClassId == classId),
                Groups = t.Groups
                    .Where(g => includeGroups && (classId == null || g.ClassId == classId) && (lecturerId == null || g.Class.LecturerId == lecturerId))
                    .OrderBy(g => g.Class.Name).ThenBy(g => g.Name)
                    .Select(g => new TopicGroupDto(g.Id, g.Name, g.ClassId, g.Class.Name))
                    .ToList()
            })
            .ToListAsync(ct);

        return topics.Select(x => new CourseTopicDto(
            x.Topic.Id,
            x.Topic.CourseId,
            x.Topic.Title,
            x.Topic.Description,
            x.Topic.Requirements,
            x.Topic.MaxGroups,
            x.Count,
            x.Topic.IsActive,
            x.Topic.CreatedAt,
            x.Topic.SemesterId,
            includeGroups ? x.Groups : null)).ToList();
    }

    public async Task<CourseTopic?> GetCourseTopicByIdAsync(Guid topicId, CancellationToken ct = default)
    {
        return await Context.CourseTopics
            .Include(t => t.Groups)
            .FirstOrDefaultAsync(t => t.Id == topicId, ct);
    }

    public async Task AddCourseTopicAsync(CourseTopic topic, CancellationToken ct = default)
    {
        await Context.CourseTopics.AddAsync(topic, ct);
    }

    public void RemoveCourseTopic(CourseTopic topic)
    {
        Context.CourseTopics.Remove(topic);
    }

    public async Task<CourseProject?> GetProjectByGroupIdAsync(Guid groupId, CancellationToken ct = default)
    {
        var run = await Context.AnalysisRuns.AsNoTracking().FirstOrDefaultAsync(r => r.GroupId == groupId, ct);
        if (run == null) return null;

        return await Context.CourseProjects
            .Include(p => p.AnalysisRun)
            .FirstOrDefaultAsync(p => p.AnalysisRunId == run.Id, ct);
    }

    public async Task<CourseProject> UpsertProjectForGroupAsync(Guid groupId, Guid courseId, string topicName, string? repoUrl, CancellationToken ct = default)
    {
        var run = await Context.AnalysisRuns.FirstOrDefaultAsync(r => r.GroupId == groupId, ct);
        if (run == null)
        {
            run = new AnalysisRun
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                RepoName = topicName,
                RepoUrl = repoUrl,
                CreatedAt = DateTime.UtcNow
            };
            await Context.AnalysisRuns.AddAsync(run, ct);
        }
        else
        {
            run.RepoName = topicName;
            if (!string.IsNullOrWhiteSpace(repoUrl))
            {
                run.RepoUrl = repoUrl;
            }
        }

        var project = await Context.CourseProjects.FirstOrDefaultAsync(p => p.AnalysisRunId == run.Id, ct);
        if (project == null)
        {
            project = new CourseProject
            {
                AnalysisRunId = run.Id,
                CourseId = courseId,
                IsApproved = null,
                Notes = null,
                DecidedAt = null
            };
            await Context.CourseProjects.AddAsync(project, ct);
        }
        else
        {
            project.CourseId = courseId;
            project.IsApproved = null;
            project.Notes = null;
            project.DecidedAt = null;
        }

        return project;
    }

    public async Task AddAuditEventAsync(Guid actorId, string actorName, Guid resourceId, string eventType, string details, CancellationToken ct = default)
    {
        var audit = new AuditEvent
        {
            Id = Guid.NewGuid(),
            ActorId = actorId,
            ActorName = actorName,
            ResourceId = resourceId,
            EventType = eventType,
            Details = details,
            Timestamp = DateTime.UtcNow
        };
        await Context.AuditEvents.AddAsync(audit, ct);
    }

    public async Task UpdateStudentClassStatusAsync(Guid classId, string studentCode, int status, CancellationToken ct = default)
    {
        var sc = await Context.StudentClasses.FirstOrDefaultAsync(x => x.ClassId == classId && x.StudentCode.ToLower() == studentCode.ToLower(), ct);
        if (sc != null)
        {
            sc.Status = status;
        }
    }
}
