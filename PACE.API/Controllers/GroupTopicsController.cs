using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record SubmitGroupTopic(
    [Required, MaxLength(255)] string Title,
    [MaxLength(4000)] string? Description,
    [Range(0, int.MaxValue - 1)] int PreviousVersion);

public record ReviewGroupTopic([Required] string Decision, [MaxLength(4000)] string? Notes);

public record GroupTopicView(
    Guid GroupId, string GroupName, int Version, string Title, string? Description,
    string Status, Guid SubmittedBy, DateTime SubmittedAt, Guid? ReviewedBy,
    DateTime? ReviewedAt, string? Notes, string? ProposalFileName, long? ProposalFileSize);

[ApiController, Authorize]
public class GroupTopicsController(AppDbContext db, TeachingAccess access) : ControllerBase
{
    private static readonly Expression<Func<GroupTopicSubmission, GroupTopicView>> View = topic =>
        new(topic.GroupId, topic.Group.Name, topic.Version, topic.Title, topic.Description,
            topic.Status.ToString(), topic.SubmittedBy, topic.SubmittedAt,
            topic.ReviewedBy, topic.ReviewedAt, topic.Notes, topic.ProposalFileName, topic.ProposalFileSize);

    private static readonly Func<GroupTopicSubmission, GroupTopicView> ToView = View.Compile();

    [HttpGet("api/group-topics/groups")]
    public async Task<IActionResult> Groups(CancellationToken ct)
    {
        var user = await access.User(ct);
        var groups = db.Groups.AsNoTracking();
        if (user.Role == UserRole.Student)
            groups = groups.Where(group => user.StudentCode != null &&
                group.Members.Any(member => member.StudentCode == user.StudentCode));
        else if (user.Role == UserRole.Lecturer)
            groups = groups.Where(group => group.Class.LecturerId == user.Id);
        else if (user.Role != UserRole.Admin)
            throw new ForbiddenException();

        var results = await groups.OrderBy(group => group.Class.Name).ThenBy(group => group.Name)
            .Select(group => new
            {
                group.Id,
                group.Name,
                className = group.Class.Name,
                group.TopicName,
                latestStatus = db.GroupTopicSubmissions.Where(topic => topic.GroupId == group.Id)
                    .OrderByDescending(topic => topic.Version)
                    .Select(topic => (GroupTopicStatus?)topic.Status).FirstOrDefault()
            }).ToListAsync(ct);
        return Ok(results.Select(group => new
        {
            group.Id, group.Name, group.className, group.TopicName, latestStatus = group.latestStatus?.ToString()
        }));
    }

    [HttpGet("api/classes/{classId:guid}/topics/pending")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Pending(Guid classId,
        [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, 100)] int limit = 20, CancellationToken ct = default)
    {
        var user = await access.User(ct);
        var courseClass = await db.Classes.FindAsync([classId], ct)
            ?? throw new NotFoundException("Class unavailable.");
        RequireReviewer(user, courseClass);

        return Ok(await db.GroupTopicSubmissions.AsNoTracking()
            .Where(topic => topic.Group.ClassId == classId && topic.Status == GroupTopicStatus.Pending)
            .OrderBy(topic => topic.SubmittedAt).ThenBy(topic => topic.GroupId)
            .Skip(offset).Take(limit).Select(View).ToListAsync(ct));
    }

    [HttpGet("api/groups/{groupId:guid}/topics")]
    public async Task<IActionResult> History(Guid groupId, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        if (user.Role == UserRole.Student)
            await RequireMember(user, groupId, ct);
        else
            RequireReviewer(user, group.Class);

        return Ok(await db.GroupTopicSubmissions.AsNoTracking().Where(topic => topic.GroupId == groupId)
            .OrderByDescending(topic => topic.Version).Select(View).ToListAsync(ct));
    }

    [HttpPost("api/groups/{groupId:guid}/topics")]
    [Authorize(Roles = "Student")]
    public IActionResult Submit(Guid groupId, SubmitGroupTopic request)
        => throw new BadRequestException(ProposalFileRules.RequiredMessage);

    [HttpPost("api/groups/{groupId:guid}/topics/with-proposal")]
    [Authorize(Roles = "Student")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProposalFileRules.MaxRequestBytes)]
    public async Task<IActionResult> SubmitWithProposal(Guid groupId, [FromForm] SubmitGroupTopicForm form, CancellationToken ct)
    {
        var proposal = await ProposalFileRules.ReadAsync(form.Proposal, ct);
        return await CreateSubmission(groupId, new SubmitGroupTopic(form.Title, form.Description, form.PreviousVersion), proposal, ct);
    }

    [HttpGet("api/groups/{groupId:guid}/topics/{version:int}/proposal")]
    public async Task<IActionResult> DownloadProposal(Guid groupId, int version, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        if (user.Role == UserRole.Student)
            await RequireMember(user, groupId, ct);
        else
            RequireReviewer(user, group.Class);

        var file = await db.GroupTopicSubmissions.AsNoTracking()
            .Where(topic => topic.GroupId == groupId && topic.Version == version && topic.ProposalFile != null)
            .Select(topic => new { topic.ProposalFile, topic.ProposalContentType, topic.ProposalFileName })
            .SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Phiên bản đề tài này không có file proposal.");

        return File(file.ProposalFile!, file.ProposalContentType ?? "application/octet-stream", file.ProposalFileName ?? "proposal");
    }

    private async Task<IActionResult> CreateSubmission(Guid groupId, SubmitGroupTopic request, ProposalFile proposal, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        await RequireMember(user, groupId, ct);

        var latest = await db.GroupTopicSubmissions.AsNoTracking().Where(topic => topic.GroupId == groupId)
            .OrderByDescending(topic => topic.Version).FirstOrDefaultAsync(ct);
        if ((latest?.Version ?? 0) != request.PreviousVersion)
            throw new ConflictException("The topic has a newer submission. Reload before submitting.");
        if (latest != null && latest.Status is not (GroupTopicStatus.ChangesRequested or GroupTopicStatus.Rejected))
            throw new ConflictException("The previous topic must be returned or rejected before resubmission.");

        var topic = new GroupTopicSubmission
        {
            Group = group,
            GroupId = groupId,
            Version = request.PreviousVersion + 1,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Status = GroupTopicStatus.Pending,
            SubmittedBy = user.Id,
            SubmittedAt = DateTime.UtcNow,
            ProposalFileName = proposal.FileName,
            ProposalContentType = proposal.ContentType,
            ProposalFileSize = proposal.Content.LongLength,
            ProposalFile = proposal.Content
        };
        db.GroupTopicSubmissions.Add(topic);
        await access.Save(ct);

        return CreatedAtAction(nameof(History), new { groupId }, ToView(topic));
    }

    [HttpPost("api/groups/{groupId:guid}/topics/{version:int}/review")]
    [Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Review(Guid groupId, int version, ReviewGroupTopic request, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        RequireReviewer(user, group.Class);

        var status = request.Decision switch
        {
            nameof(GroupTopicStatus.Approved) => GroupTopicStatus.Approved,
            nameof(GroupTopicStatus.ChangesRequested) => GroupTopicStatus.ChangesRequested,
            nameof(GroupTopicStatus.Rejected) => GroupTopicStatus.Rejected,
            _ => throw new BadRequestException("Invalid topic decision.")
        };
        if (status != GroupTopicStatus.Approved && string.IsNullOrWhiteSpace(request.Notes))
            throw new BadRequestException("Provide a reason when returning or rejecting a topic.");

        var topic = await db.GroupTopicSubmissions.FindAsync([groupId, version], ct)
            ?? throw new NotFoundException("Topic submission unavailable.");
        if (topic.Status != GroupTopicStatus.Pending)
            throw new ConflictException("This version has already been reviewed.");

        topic.Status = status;
        topic.ReviewedBy = user.Id;
        topic.ReviewedAt = DateTime.UtcNow;
        topic.Notes = request.Notes?.Trim();

        // Duyệt xong thì nhóm phải có dự án (AnalysisRun) để bước "Liên kết repo" mở ra.
        if (status == GroupTopicStatus.Approved)
        {
            group.TopicName = topic.Title;
            await EnsureProject(group, topic.Title, approved: true, ct);
        }

        await access.Save(ct);
        return NoContent();
    }

    /// <summary>
    /// Tạo dự án cho nhóm nếu đề tài đã được chấp nhận mà chưa có (dữ liệu duyệt trước bản sửa này).
    /// Đề tài được chấp nhận khi: lần gửi mới nhất đã Approved, hoặc nhóm chọn đề tài mẫu.
    /// </summary>
    [HttpPost("api/groups/{groupId:guid}/project/ensure")]
    public async Task<IActionResult> EnsureGroupProject(Guid groupId, CancellationToken ct)
    {
        var user = await access.User(ct);
        var group = await LoadGroup(groupId, ct);
        if (user.Role == UserRole.Student)
            await RequireMember(user, groupId, ct);
        else
            RequireReviewer(user, group.Class);

        var latest = await db.GroupTopicSubmissions.AsNoTracking().Where(topic => topic.GroupId == groupId)
            .OrderByDescending(topic => topic.Version).FirstOrDefaultAsync(ct);

        string? title = null;
        if (latest != null)
        {
            if (latest.Status != GroupTopicStatus.Approved)
                throw new ConflictException("Đề tài của nhóm chưa được giảng viên duyệt.");
            title = latest.Title;
        }
        else if (group.CourseTopicId != null && !string.IsNullOrWhiteSpace(group.TopicName))
        {
            title = group.TopicName;
        }

        if (title == null)
            throw new ConflictException("Nhóm chưa có đề tài được chấp nhận.");

        var runId = await EnsureProject(group, title, approved: true, ct);
        await access.Save(ct);
        return Ok(new { analysisRunId = runId });
    }

    private async Task<Guid> EnsureProject(Group group, string title, bool approved, CancellationToken ct)
    {
        var run = await db.AnalysisRuns.FirstOrDefaultAsync(r => r.GroupId == group.Id, ct);
        if (run == null)
        {
            run = new AnalysisRun
            {
                Id = Guid.NewGuid(),
                GroupId = group.Id,
                RepoName = title,
                CreatedAt = DateTime.UtcNow
            };
            db.AnalysisRuns.Add(run);
        }
        else if (string.IsNullOrWhiteSpace(run.RepoUrl))
        {
            run.RepoName = title;
        }

        var project = await db.CourseProjects.FirstOrDefaultAsync(p => p.AnalysisRunId == run.Id, ct);
        if (project == null)
        {
            project = new CourseProject { AnalysisRunId = run.Id, CourseId = group.Class.CourseId };
            db.CourseProjects.Add(project);
        }
        if (approved)
        {
            project.IsApproved = true;
            project.DecidedAt ??= DateTime.UtcNow;
        }
        return run.Id;
    }

    private async Task<Group> LoadGroup(Guid id, CancellationToken ct) =>
        await db.Groups.Include(group => group.Class).SingleOrDefaultAsync(group => group.Id == id, ct)
        ?? throw new NotFoundException("Group unavailable.");

    internal static void RequireReviewer(User user, Class courseClass)
    {
        if (user.Role != UserRole.Admin &&
            !(user.Role == UserRole.Lecturer && courseClass.LecturerId == user.Id))
            throw new ForbiddenException();
    }

    private async Task RequireMember(User user, Guid groupId, CancellationToken ct)
    {
        if (user.Role != UserRole.Student || user.StudentCode == null ||
            !await db.GroupMembers.AnyAsync(member => member.GroupId == groupId && member.StudentCode == user.StudentCode, ct))
            throw new ForbiddenException();
    }
}
