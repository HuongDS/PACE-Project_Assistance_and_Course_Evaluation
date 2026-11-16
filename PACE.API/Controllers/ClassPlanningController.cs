using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Excel;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record SaveClassScheduleItem(
    [Required, RegularExpression("^(Milestone|Deadline)$")] string Kind,
    [Required, MaxLength(255)] string Title,
    [MaxLength(4000)] string? Description,
    [Required] DateTimeOffset? DueAt);

public record ClassScheduleItemView(Guid Id, Guid ClassId, ClassScheduleItemKind Kind, string Title, string? Description, DateTime DueAt);

public record SaveCourseAnnouncement(
    [Required, MaxLength(255)] string Title,
    [Required, MaxLength(8000)] string Body);

public record CourseAnnouncementView(
    Guid Id, Guid ClassId, string Title, string Body, Guid CreatedBy, DateTime CreatedAt, DateTime? UpdatedAt);

public record AssessmentRoundConfiguration(
    [Required, MaxLength(100)] List<string> Deliverables,
    [Required, MinLength(1), MaxLength(100)] List<SessionCriterion> Rubric,
    [MaxLength(8000)] string? GradingGuide,
    [Required, MaxLength(100)] List<string> Clos,
    [Required, MaxLength(100)] List<string> Topics,
    [MaxLength(8000)] string? AiQuestioningInstructions);

public record SaveClassAssessmentRound(
    [Required, MaxLength(255)] string Title,
    [Range(0.01, 100.0)] decimal Weight,
    [Required] DateTimeOffset? OpensAt,
    [Required] DateTimeOffset? ClosesAt,
    [Required] AssessmentRoundConfiguration Configuration);

public record ClassAssessmentRoundView(
    Guid Id, Guid MilestoneId, string Title, decimal Weight,
    DateTime OpensAt, DateTime ClosesAt, AssessmentRoundConfiguration Configuration);

public record AssessmentRoundRevisionView(
    Guid Id, Guid RoundId, int Version, string Title, decimal Weight,
    DateTime OpensAt, DateTime ClosesAt, DateTime DueAt, Guid PublishedBy, DateTime PublishedAt,
    AssessmentRoundConfiguration Configuration);

public record PublishAssessmentRound([Range(0, int.MaxValue - 1)] int PreviousVersion);

[ApiController, Authorize, Route("api/classes/{classId:guid}")]
public class ClassPlanningController(AppDbContext db, TeachingAccess access) : ControllerBase
{
    private const int UploadLimit = 5 * 1024 * 1024;

    [HttpGet("milestones")]
    public async Task<ActionResult<IReadOnlyList<ClassScheduleItemView>>> Schedule(
        Guid classId, [FromQuery, RegularExpression("^(Milestone|Deadline)$")] string? kind, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: false, ct);
        var query = db.ClassMilestones.AsNoTracking().Where(x => x.ClassId == classId);
        if (kind != null)
        {
            var parsed = Enum.Parse<ClassScheduleItemKind>(kind);
            query = query.Where(x => x.Kind == parsed);
        }

        var items = await query.OrderBy(x => x.DueAt).ThenBy(x => x.Id).ToListAsync(ct);
        return Ok(items.Select(ScheduleView));
    }

    [HttpGet("milestones/{id:guid}")]
    public async Task<ActionResult<ClassScheduleItemView>> GetScheduleItem(Guid classId, Guid id, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: false, ct);
        return Ok(ScheduleView(await FindScheduleItem(classId, id, ct)));
    }

    [HttpPost("milestones"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassScheduleItemView>> CreateScheduleItem(
        Guid classId, SaveClassScheduleItem request, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        var item = ScheduleItem(classId, request.Kind, request.Title, request.Description, request.DueAt!.Value);
        db.ClassMilestones.Add(item);
        await access.Save(ct);
        return CreatedAtAction(nameof(GetScheduleItem), new { classId, id = item.Id }, ScheduleView(item));
    }

    [HttpPut("milestones/{id:guid}"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassScheduleItemView>> UpdateScheduleItem(
        Guid classId, Guid id, SaveClassScheduleItem request, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        var item = await FindScheduleItem(classId, id, ct);
        var update = ScheduleItem(classId, request.Kind, request.Title, request.Description, request.DueAt!.Value);
        if (item.Kind != update.Kind)
            throw new BadRequestException("The schedule item kind cannot change after creation.");

        item.Title = update.Title;
        item.Description = update.Description;
        item.DueAt = update.DueAt;
        await access.Save(ct);
        return Ok(ScheduleView(item));
    }

    [HttpDelete("milestones/{id:guid}"), Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> DeleteScheduleItem(Guid classId, Guid id, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        var item = await FindScheduleItem(classId, id, ct);
        if (await db.ClassAssessmentRounds.AnyAsync(x => x.MilestoneId == id, ct))
            throw new ConflictException("Remove the milestone's assessment rounds before deleting it.");

        db.ClassMilestones.Remove(item);
        await access.Save(ct);
        return NoContent();
    }

    [HttpPost("milestones/import"), Authorize(Roles = Roles.Staff)]
    [RequestSizeLimit(UploadLimit + 65536), RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    public async Task<ActionResult<IReadOnlyList<ClassScheduleItemView>>> ImportSchedule(
        Guid classId, IFormFile file, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        if (file.Length is <= 0 or > UploadLimit)
            throw new BadRequestException("Upload a nonempty file of at most 5 MiB.");

        using var stream = file.OpenReadStream();
        var rows = ClassScheduleImportReader.Read(stream, Path.GetExtension(file.FileName).ToLowerInvariant(), ct);
        if (rows.Count == 0)
            throw new BadRequestException("The import must contain at least one data row.");

        var items = new List<ClassMilestone>(rows.Count);
        foreach (var row in rows)
        {
            var value = row.DueAt.Trim();
            if (!Regex.IsMatch(value, @"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}(:\d{2}(\.\d{1,7})?)?(Z|[+-]\d{2}:\d{2})$")
                || !DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dueAt))
                throw new BadRequestException($"Row {row.Line}: dueAt must be an ISO 8601 timestamp with a timezone.");

            try
            {
                items.Add(ScheduleItem(classId, row.Kind, row.Title, row.Description, dueAt));
            }
            catch (BadRequestException ex)
            {
                throw new BadRequestException($"Row {row.Line}: {ex.Message}");
            }
        }

        db.ClassMilestones.AddRange(items);
        await access.Save(ct);
        return StatusCode(StatusCodes.Status201Created, items.Select(ScheduleView).ToArray());
    }

    [HttpGet("milestones/{id:guid}/assessment-rounds"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<IReadOnlyList<ClassAssessmentRoundView>>> Rounds(Guid classId, Guid id, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        await RequireMilestone(classId, id, ct);
        var rounds = await db.ClassAssessmentRounds.AsNoTracking().Where(x => x.MilestoneId == id)
            .OrderBy(x => x.OpensAt).ThenBy(x => x.Id).ToListAsync(ct);
        return Ok(rounds.Select(RoundView));
    }

    [HttpGet("milestones/{id:guid}/assessment-rounds/{roundId:guid}"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassAssessmentRoundView>> GetRound(Guid classId, Guid id, Guid roundId, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        await RequireMilestone(classId, id, ct);
        return Ok(RoundView(await FindRound(id, roundId, ct)));
    }

    [HttpPost("milestones/{id:guid}/assessment-rounds"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassAssessmentRoundView>> CreateRound(
        Guid classId, Guid id, SaveClassAssessmentRound request, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        await RequireMilestone(classId, id, ct);

        var round = new ClassAssessmentRound { MilestoneId = id };
        SetRound(round, request);
        db.ClassAssessmentRounds.Add(round);
        await access.Save(ct);
        return CreatedAtAction(nameof(GetRound), new { classId, id, roundId = round.Id }, RoundView(round));
    }

    [HttpPut("milestones/{id:guid}/assessment-rounds/{roundId:guid}"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassAssessmentRoundView>> UpdateRound(
        Guid classId, Guid id, Guid roundId, SaveClassAssessmentRound request, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        await RequireMilestone(classId, id, ct);
        var round = await FindRound(id, roundId, ct);
        SetRound(round, request);
        await access.Save(ct);
        return Ok(RoundView(round));
    }

    [HttpDelete("milestones/{id:guid}/assessment-rounds/{roundId:guid}"), Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> DeleteRound(Guid classId, Guid id, Guid roundId, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        await RequireMilestone(classId, id, ct);
        var round = await FindRound(id, roundId, ct);
        if (await db.AssessmentRoundRevisions.AnyAsync(x => x.RoundId == roundId, ct))
            throw new ConflictException("Published rounds must remain available for assessment history.");

        db.ClassAssessmentRounds.Remove(round);
        await access.Save(ct);
        return NoContent();
    }

    [HttpGet("milestones/{id:guid}/assessment-rounds/{roundId:guid}/revisions"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<IReadOnlyList<AssessmentRoundRevisionView>>> Revisions(
        Guid classId, Guid id, Guid roundId, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        await RequireMilestone(classId, id, ct);
        await FindRound(id, roundId, ct);
        var revisions = await db.AssessmentRoundRevisions.AsNoTracking()
            .Where(x => x.RoundId == roundId).OrderByDescending(x => x.Version).ToListAsync(ct);
        return Ok(revisions.Select(RevisionView));
    }

    [HttpPost("milestones/{id:guid}/assessment-rounds/{roundId:guid}/publish"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<AssessmentRoundRevisionView>> Publish(
        Guid classId, Guid id, Guid roundId, PublishAssessmentRound request, CancellationToken ct)
    {
        var user = await RequireClassAccess(classId, write: true, ct);
        var milestone = await RequireMilestone(classId, id, ct);
        var round = await FindRound(id, roundId, ct);
        var version = await db.AssessmentRoundRevisions.Where(x => x.RoundId == roundId)
            .MaxAsync(x => (int?)x.Version, ct) ?? 0;
        if (version != request.PreviousVersion)
            throw new ConflictException("The round already has a newer published revision. Refresh before publishing.");

        var revision = new AssessmentRoundRevision
        {
            RoundId = roundId, Version = version + 1, Title = round.Title, Weight = round.Weight,
            OpensAt = round.OpensAt, ClosesAt = round.ClosesAt, DueAt = milestone.DueAt,
            PublishedBy = user.Id, PublishedAt = DateTime.UtcNow, ConfigurationJson = round.ConfigurationJson
        };
        db.AssessmentRoundRevisions.Add(revision);
        await access.Save(ct);
        return CreatedAtAction(nameof(Revisions), new { classId, id, roundId }, RevisionView(revision));
    }

    [HttpGet("announcements")]
    public async Task<ActionResult<IReadOnlyList<CourseAnnouncementView>>> Announcements(
        Guid classId, [FromQuery, Range(0, int.MaxValue)] int offset = 0,
        [FromQuery, Range(1, 100)] int limit = 20, CancellationToken ct = default)
    {
        await RequireClassAccess(classId, write: false, ct);
        var items = await db.CourseAnnouncements.AsNoTracking().Where(x => x.ClassId == classId)
            .OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip(offset).Take(limit).ToListAsync(ct);
        return Ok(items.Select(AnnouncementView));
    }

    [HttpGet("announcements/{id:guid}")]
    public async Task<ActionResult<CourseAnnouncementView>> GetAnnouncement(Guid classId, Guid id, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: false, ct);
        return Ok(AnnouncementView(await FindAnnouncement(classId, id, ct)));
    }

    [HttpPost("announcements"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<CourseAnnouncementView>> CreateAnnouncement(
        Guid classId, SaveCourseAnnouncement request, CancellationToken ct)
    {
        var user = await RequireClassAccess(classId, write: true, ct);
        var item = new CourseAnnouncement
        {
            ClassId = classId, CreatedBy = user.Id, Title = request.Title.Trim(), Body = request.Body.Trim()
        };
        db.CourseAnnouncements.Add(item);
        await access.Save(ct);
        return CreatedAtAction(nameof(GetAnnouncement), new { classId, id = item.Id }, AnnouncementView(item));
    }

    [HttpPut("announcements/{id:guid}"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<CourseAnnouncementView>> UpdateAnnouncement(
        Guid classId, Guid id, SaveCourseAnnouncement request, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        var item = await FindAnnouncement(classId, id, ct);
        item.Title = request.Title.Trim();
        item.Body = request.Body.Trim();
        item.UpdatedAt = DateTime.UtcNow;
        await access.Save(ct);
        return Ok(AnnouncementView(item));
    }

    [HttpDelete("announcements/{id:guid}"), Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> DeleteAnnouncement(Guid classId, Guid id, CancellationToken ct)
    {
        await RequireClassAccess(classId, write: true, ct);
        db.CourseAnnouncements.Remove(await FindAnnouncement(classId, id, ct));
        await access.Save(ct);
        return NoContent();
    }

    private async Task<User> RequireClassAccess(Guid classId, bool write, CancellationToken ct)
    {
        var user = await access.User(ct);
        var scope = await db.Classes.AsNoTracking().Where(x => x.Id == classId)
            .Select(x => new
            {
                x.LecturerId,
                Enrolled = user.StudentCode != null &&
                    x.StudentClasses.Any(s => s.StudentCode == user.StudentCode && s.Status == 1)
            }).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Class unavailable.");

        if (user.Role == UserRole.Admin ||
            (user.Role == UserRole.Lecturer && scope.LecturerId == user.Id) ||
            (!write && user.Role == UserRole.Student && scope.Enrolled))
            return user;

        throw new ForbiddenException();
    }

    private async Task<ClassMilestone> FindScheduleItem(Guid classId, Guid id, CancellationToken ct) =>
        await db.ClassMilestones.SingleOrDefaultAsync(x => x.ClassId == classId && x.Id == id, ct)
        ?? throw new NotFoundException("Schedule item unavailable.");

    private async Task<ClassMilestone> RequireMilestone(Guid classId, Guid id, CancellationToken ct)
    {
        var milestone = await FindScheduleItem(classId, id, ct);
        if (milestone.Kind != ClassScheduleItemKind.Milestone)
            throw new BadRequestException("Assessment rounds must be linked to a milestone.");
        return milestone;
    }

    private async Task<ClassAssessmentRound> FindRound(Guid milestoneId, Guid id, CancellationToken ct) =>
        await db.ClassAssessmentRounds.SingleOrDefaultAsync(x => x.MilestoneId == milestoneId && x.Id == id, ct)
        ?? throw new NotFoundException("Assessment round unavailable.");

    private async Task<CourseAnnouncement> FindAnnouncement(Guid classId, Guid id, CancellationToken ct) =>
        await db.CourseAnnouncements.SingleOrDefaultAsync(x => x.ClassId == classId && x.Id == id, ct)
        ?? throw new NotFoundException("Announcement unavailable.");

    private static ClassMilestone ScheduleItem(Guid classId, string kind, string title, string? description, DateTimeOffset dueAt)
    {
        if (!Enum.TryParse<ClassScheduleItemKind>(kind.Trim(), ignoreCase: true, out var parsed)
            || !string.Equals(parsed.ToString(), kind.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Kind must be Milestone or Deadline.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 255 || description?.Trim().Length > 4000)
            throw new BadRequestException("Title must contain 1–255 characters and description at most 4000.");

        return new ClassMilestone
        {
            ClassId = classId, Kind = parsed, Title = title.Trim(),
            Description = description?.Trim(), DueAt = dueAt.UtcDateTime
        };
    }

    private static void SetRound(ClassAssessmentRound round, SaveClassAssessmentRound request)
    {
        if (request.ClosesAt <= request.OpensAt)
            throw new BadRequestException("Closing time must be after opening time.");
        if (decimal.Round(request.Weight, 2) != request.Weight)
            throw new BadRequestException("Weight must have at most two decimal places.");

        var configuration = request.Configuration;
        if (configuration.Rubric.Any(x => x == null || x.Id == Guid.Empty) ||
            configuration.Rubric.Select(x => x.Id).Distinct().Count() != configuration.Rubric.Count ||
            configuration.Rubric.Sum(x => x.Weight) != 100)
            throw new BadRequestException("Rubric criterion IDs must be nonempty and unique, with weights totaling 100.");

        ValidateEntries(configuration.Deliverables, 500, "Deliverables");
        ValidateEntries(configuration.Clos, 100, "CLOs");
        ValidateEntries(configuration.Topics, 255, "Topics");

        round.Title = request.Title.Trim();
        round.Weight = request.Weight;
        round.OpensAt = request.OpensAt!.Value.UtcDateTime;
        round.ClosesAt = request.ClosesAt!.Value.UtcDateTime;
        round.ConfigurationJson = JsonSerializer.Serialize(configuration);
    }

    private static void ValidateEntries(List<string> values, int maxLength, string label)
    {
        if (values.Any(x => string.IsNullOrWhiteSpace(x) || x.Length > maxLength))
            throw new BadRequestException($"{label} entries must contain 1–{maxLength} characters.");
    }

    private static ClassScheduleItemView ScheduleView(ClassMilestone item) =>
        new(item.Id, item.ClassId, item.Kind, item.Title, item.Description, item.DueAt);

    private static ClassAssessmentRoundView RoundView(ClassAssessmentRound round) =>
        new(round.Id, round.MilestoneId, round.Title, round.Weight, round.OpensAt, round.ClosesAt,
            JsonSerializer.Deserialize<AssessmentRoundConfiguration>(round.ConfigurationJson)!);

    private static AssessmentRoundRevisionView RevisionView(AssessmentRoundRevision revision) =>
        new(revision.Id, revision.RoundId, revision.Version, revision.Title, revision.Weight,
            revision.OpensAt, revision.ClosesAt, revision.DueAt, revision.PublishedBy, revision.PublishedAt,
            JsonSerializer.Deserialize<AssessmentRoundConfiguration>(revision.ConfigurationJson)!);

    private static CourseAnnouncementView AnnouncementView(CourseAnnouncement item) =>
        new(item.Id, item.ClassId, item.Title, item.Body, item.CreatedBy, item.CreatedAt, item.UpdatedAt);
}
