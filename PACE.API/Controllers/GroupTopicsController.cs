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
    DateTime? ReviewedAt, string? Notes);

[ApiController, Authorize]
public class GroupTopicsController(AppDbContext db, TeachingAccess access) : ControllerBase
{
    private static readonly Expression<Func<GroupTopicSubmission, GroupTopicView>> View = topic =>
        new(topic.GroupId, topic.Group.Name, topic.Version, topic.Title, topic.Description,
            topic.Status.ToString(), topic.SubmittedBy, topic.SubmittedAt,
            topic.ReviewedBy, topic.ReviewedAt, topic.Notes);

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
                group.ClassId,
                className = group.Class.Name,
                group.TopicName,
                latestStatus = db.GroupTopicSubmissions.Where(topic => topic.GroupId == group.Id)
                    .OrderByDescending(topic => topic.Version)
                    .Select(topic => (GroupTopicStatus?)topic.Status).FirstOrDefault()
            }).ToListAsync(ct);
        return Ok(results.Select(group => new
        {
            group.Id, group.Name, group.ClassId, group.className, group.TopicName, latestStatus = group.latestStatus?.ToString()
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
    public async Task<IActionResult> Submit(Guid groupId, SubmitGroupTopic request, CancellationToken ct)
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
            SubmittedAt = DateTime.UtcNow
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
        await access.Save(ct);
        return NoContent();
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
