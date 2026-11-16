using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Interfaces;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record CreateCourseRequest([Required, MaxLength(40)] string Code, [Required, MaxLength(255)] string Name, int Credits = 3, string? Semester = null, Guid? LecturerId = null);
public record TopicDecision(bool IsApproved, [MaxLength(4000)] string? Notes);
public record SessionCriterion(Guid Id, [Required, MaxLength(255)] string Title, [MaxLength(4000)] string Description, [Range(0.01, 1000)] decimal MaxScore, [Range(0.01, 100)] decimal Weight);
public record CreateSessionRequest(Guid ProjectId, [Required, MaxLength(255)] string Title, DateTime ScheduledStart, DateTime ScheduledEnd, [Required, MinLength(1), MaxLength(100)] List<Guid> StudentIds, [MinLength(1), MaxLength(100)] List<SessionCriterion>? Criteria, Guid? OralExamId = null, Guid? AssessmentRoundRevisionId = null);
public record ScoreInput(Guid CriterionId, [Range(0, 1000)] decimal Score, [MaxLength(4000)] string? Feedback);
public record EvaluateRequest([Required, MinLength(1), MaxLength(100)] List<ScoreInput> Evaluations);

public record OralQuestionSnapshot(Guid Id, string Content, string? ReferenceCommit);
public record OralEvidenceInput(Guid QuestionId, [Required, MaxLength(8000)] string AnswerNotes,
    [MaxLength(4000)] string? FollowUp = null);
public record SaveOralEvidenceRequest([Required, MinLength(1), MaxLength(100)] List<OralEvidenceInput> Answers);

public static class SessionRules
{
    public static void Require(string current, string expected) { if (current != expected) throw new ConflictException($"Session must be {expected}; current status is {current}."); }
    public static void ValidateScores(List<SessionCriterion> rubric, List<ScoreInput> scores)
    {
        if (scores.Select(x => x.CriterionId).Distinct().Count() != scores.Count || scores.Count != rubric.Count ||
          scores.Any(x => !rubric.Any(c => c.Id == x.CriterionId && x.Score >= 0 && x.Score <= c.MaxScore)))
            throw new BadRequestException("Provide exactly one valid score per session rubric criterion.");
    }
}

// Resource checks use current persisted user assignments, not client identity or stale JWT assignments.
public class TeachingAccess(AppDbContext db, ICurrentUserService current)
{
    public async Task<User> User(CancellationToken ct) => await db.Users.SingleOrDefaultAsync(x => x.Id == current.UserId, ct) ?? throw new UnauthorizedException("Account unavailable.");
    public async Task<CourseProject> Project(Guid id, CancellationToken ct)
    {
        var run = await db.AnalysisRuns
            .Include(r => r.Group)
                .ThenInclude(g => g!.Class)
                    .ThenInclude(c => c!.Course)
            .Include(r => r.Group)
                .ThenInclude(g => g!.Members)
            .Include(r => r.Users)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

        var p = await db.CourseProjects
            .Include(x => x.Course)
            .Include(x => x.AnalysisRun)
            .SingleOrDefaultAsync(x => x.AnalysisRunId == id, ct);

        if (run == null && p == null) throw new NotFoundException("Project not assigned to a course.");

        var course = run?.Group?.Class?.Course ?? p?.Course;
        if (course == null && p == null) throw new NotFoundException("Project not assigned to a course.");

        if (p == null)
        {
            p = new CourseProject
            {
                AnalysisRunId = run!.Id,
                AnalysisRun = run,
                CourseId = course!.Id,
                Course = course
            };
            db.CourseProjects.Add(p);
        }
        else
        {
            if (run != null) p.AnalysisRun = run;
            if (course != null && p.CourseId != course.Id)
            {
                p.CourseId = course.Id;
                p.Course = course;
            }
        }

        var u = await User(ct);
        bool isAdmin = u.Role == UserRole.Admin;
        bool isLecturer = u.Role == UserRole.Lecturer && (
            (run?.Group?.Class?.LecturerId == u.Id) ||
            (p.AnalysisRun?.Group?.Class?.LecturerId == u.Id) ||
            (p.Course != null && p.Course.Classes.Any(cl => cl.LecturerId == u.Id)) ||
            (run?.Group?.Class?.Course != null && run.Group.Class.Course.Classes.Any(cl => cl.LecturerId == u.Id))
        );
        bool isStudent = u.Role == UserRole.Student && (
            u.AnalysisRunId == id ||
            (run != null && (
                run.Users.Any(ru => ru.Id == u.Id) ||
                (run.Group != null && run.Group.Members.Any(m => m.StudentCode == u.StudentCode))
            ))
        );

        if (!isAdmin && !isLecturer && !isStudent) throw new ForbiddenException();
        return p;
    }
    public async Task Staff(CourseProject p, CancellationToken ct)
    {
        var u = await User(ct);
        var classLecturerId = p.AnalysisRun?.Group?.Class?.LecturerId;
        bool isAuthorizedLecturer = u.Role == UserRole.Lecturer && (
            classLecturerId == u.Id ||
            (p.Course != null && p.Course.Classes.Any(cl => cl.LecturerId == u.Id))
        );
        if (u.Role != UserRole.Admin && !isAuthorizedLecturer) throw new ForbiddenException();
    }
    public void Audit(Guid resource, string type, string detail) => db.AuditEvents.Add(new AuditEvent { ActorId = current.UserId, ActorName = current.Email, ResourceId = resource, EventType = type, Details = detail });
    public async Task Save(CancellationToken ct)
    {
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateConcurrencyException) { throw new ConflictException("Resource changed concurrently; reload and retry."); }
    }
}

[ApiController, Route("api/courses"), Authorize]
public class CoursesController(AppDbContext db, TeachingAccess access, ILogger<CoursesController> logger) : ControllerBase
{
    private IQueryable<Course> Visible(User u) => db.Courses.AsNoTracking().Where(c =>
            u.Role == UserRole.Admin ||
            (u.Role == UserRole.Lecturer && c.Classes.Any(cl => cl.LecturerId == u.Id)) ||
            (u.Role == UserRole.Student && (
                c.Projects.Any(p => p.AnalysisRunId == u.AnalysisRunId) ||
                c.Classes.Any(cl => cl.Groups.Any(g =>
                    g.AnalysisRuns.Any(r => r.Id == u.AnalysisRunId) ||
                    g.Members.Any(m => m.StudentCode == u.StudentCode))))));

    private IQueryable<object> Details(IQueryable<Course> q, User user) => q.Select(c => new
    {
        c.Id,
        c.Code,
        c.Name,
        semester = c.Classes.OrderByDescending(cl => cl.StartDate).Select(cl => cl.Semester != null ? cl.Semester.Name : "").FirstOrDefault() ?? "",
        lecturerId = c.Classes.Where(cl => cl.LecturerId.HasValue).Select(cl => cl.LecturerId).FirstOrDefault(),
        lecturerName = c.Classes.Where(cl => cl.Lecturer != null).Select(cl => cl.Lecturer!.FullName ?? cl.Lecturer!.Email).FirstOrDefault() ?? "",
        totalGroups = c.Classes.SelectMany(cl => cl.Groups).Count() > 0
            ? c.Classes.SelectMany(cl => cl.Groups).Count()
            : c.Projects.Count,
        classes = c.Classes.Select(cl => new
        {
            id = cl.Id,
            name = cl.Name,
            semesterId = cl.SemesterId,
            semesterName = cl.Semester != null ? cl.Semester.Name : "",
            studentCount = cl.StudentClasses.Count,
            groupCount = cl.Groups.Count,
            lecturerId = cl.LecturerId,
            lecturerName = cl.Lecturer != null ? (cl.Lecturer.FullName ?? cl.Lecturer.Email) : ""
        }),
        milestones = db.ClassMilestones.Where(item => item.Class.CourseId == c.Id &&
                (user.Role == UserRole.Admin ||
                 (user.Role == UserRole.Lecturer && item.Class.LecturerId == user.Id) ||
                 (user.Role == UserRole.Student && user.StudentCode != null && item.Class.StudentClasses.Any(
                     student => student.StudentCode == user.StudentCode && student.Status == 1))))
            .OrderBy(item => item.DueAt).ThenBy(item => item.Id)
            .Select(item => new ClassScheduleItemView(item.Id, item.ClassId, item.Kind, item.Title, item.Description, item.DueAt)).ToList(),
        projects = c.Classes.SelectMany(cl => cl.Groups).Any(g => g.AnalysisRuns.Any())
            ? c.Classes.SelectMany(cl => cl.Groups)
                .SelectMany(g => g.AnalysisRuns.Select(r => new
                {
                    id = r.Id,
                    name = g.TopicName ?? r.RepoName ?? g.Name,
                    topicName = g.TopicName,
                    groupId = (Guid?)g.Id,
                    groupName = (string?)g.Name,
                    classId = (Guid?)g.ClassId,
                    className = (string?)g.Class.Name,
                    repoUrl = r.RepoUrl,
                    hasTopicSubmissions = db.GroupTopicSubmissions.Any(topic => topic.GroupId == g.Id),
                    isApproved = c.Projects.Where(p => p.AnalysisRunId == r.Id).Select(p => (bool?)p.IsApproved).FirstOrDefault(),
                    notes = c.Projects.Where(p => p.AnalysisRunId == r.Id).Select(p => p.Notes).FirstOrDefault(),
                    decidedAt = c.Projects.Where(p => p.AnalysisRunId == r.Id).Select(p => p.DecidedAt).FirstOrDefault(),
                    createdAt = (DateTime?)r.CreatedAt,
                    students = r.Users.Where(u => u.Role == UserRole.Student).Select(u => new { u.Id, u.Email })
                }))
            : c.Projects.Select(p => new
            {
                id = p.AnalysisRunId,
                name = p.AnalysisRun.Group != null && !string.IsNullOrEmpty(p.AnalysisRun.Group.TopicName) ? p.AnalysisRun.Group.TopicName : (p.AnalysisRun.RepoName ?? ""),
                topicName = p.AnalysisRun.Group != null ? p.AnalysisRun.Group.TopicName : (string?)null,
                groupId = p.AnalysisRun.GroupId,
                groupName = p.AnalysisRun.Group != null ? p.AnalysisRun.Group.Name : (string?)null,
                classId = p.AnalysisRun.Group != null ? (Guid?)p.AnalysisRun.Group.ClassId : null,
                className = p.AnalysisRun.Group != null ? p.AnalysisRun.Group.Class.Name : (string?)null,
                repoUrl = p.AnalysisRun.RepoUrl,
                hasTopicSubmissions = db.GroupTopicSubmissions.Any(topic => topic.GroupId == p.AnalysisRun.GroupId),
                isApproved = p.IsApproved,
                notes = p.Notes,
                decidedAt = p.DecidedAt,
                createdAt = (DateTime?)p.AnalysisRun.CreatedAt,
                students = p.AnalysisRun.Users.Where(u => u.Role == UserRole.Student).Select(u => new { u.Id, u.Email })
            })
    });
    [ProducesResponseType(typeof(CourseView[]), 200)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, int.MaxValue)] int page = 1,
        [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default)
    {
        var user = await access.User(ct);
        var courses = Visible(user).OrderBy(c => c.Code).Skip((page - 1) * pageSize).Take(pageSize);
        return Ok(await Details(courses, user).ToListAsync(ct));
    }
    [ProducesResponseType(typeof(CourseView), 200)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var user = await access.User(ct);
        return Ok(await Details(Visible(user).Where(c => c.Id == id), user).SingleOrDefaultAsync(ct)
            ?? throw new NotFoundException("Course unavailable."));
    }
    [ProducesResponseType(typeof(ResourceCreated), 201)]
    [HttpPost, Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Create(CreateCourseRequest request, CancellationToken ct)
    {
        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        if (await db.Courses.AnyAsync(c => c.Code.ToUpper() == normalizedCode, ct))
            return Conflict(new { error = $"Môn học với mã '{request.Code}' đã tồn tại trong danh mục." });

        var c = new Course
        {
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Credits = request.Credits > 0 ? request.Credits : 3
        };
        db.Courses.Add(c);
        access.Audit(c.Id, "COURSE_CREATED", c.Code);
        await access.Save(ct);
        return CreatedAtAction(nameof(Get), new { id = c.Id }, new { c.Id });
    }
    [HttpPut("{id:guid}/projects/{projectId:guid}"), Authorize(Roles = "Admin")]
    public async Task<IActionResult> Assign(Guid id, Guid projectId, [FromQuery] Guid? groupId = null, CancellationToken ct = default)
    {
        var course = await db.Courses.Include(c => c.Classes).ThenInclude(cl => cl.Groups).FirstOrDefaultAsync(c => c.Id == id, ct);
        var run = await db.AnalysisRuns.FindAsync([projectId], ct);
        if (course == null || run == null) return NotFound();
        var p = await db.CourseProjects.FindAsync([projectId], ct);
        if (p != null) return Conflict(new { error = "Project already assigned; reassignment requires reviewing its existing sessions." });

        if (groupId.HasValue)
        {
            var targetGroup = course.Classes.SelectMany(cl => cl.Groups).FirstOrDefault(g => g.Id == groupId.Value)
                ?? await db.Groups.FindAsync([groupId.Value], ct);
            if (targetGroup != null)
            {
                run.GroupId = targetGroup.Id;
            }
        }
        else
        {
            var matchedGroup = course.Classes.SelectMany(cl => cl.Groups)
                .FirstOrDefault(g => g.Name == run.RepoName || g.TopicName == run.RepoName)
                ?? course.Classes.SelectMany(cl => cl.Groups).FirstOrDefault();
            if (matchedGroup != null)
            {
                run.GroupId = matchedGroup.Id;
            }
        }

        db.CourseProjects.Add(new CourseProject { CourseId = id, AnalysisRunId = projectId });
        access.Audit(projectId, "PROJECT_ASSIGNED", id.ToString());
        await access.Save(ct);
        return NoContent();
    }
    [HttpPost("projects/{projectId:guid}/topic-approval"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Approve(Guid projectId, TopicDecision request, CancellationToken ct)
    {
        var p = await access.Project(projectId, ct);
        await RequireTopicReviewer(p, ct);
        if (p.AnalysisRun?.GroupId is Guid groupId &&
            await db.GroupTopicSubmissions.AnyAsync(topic => topic.GroupId == groupId, ct))
            throw new ConflictException("Review this group's submitted topic version using the group topic endpoint.");


        // Quy tắc: Nếu duyệt rồi là không được sửa, tương ứng với các đề tài bị từ chối cũng vậy
        if (p.IsApproved.HasValue)
        {
            return BadRequest(new { error = "Đề tài này đã có kết quả xét duyệt (Đã duyệt hoặc Bị từ chối) và không thể thay đổi quyết định." });
        }

        p.IsApproved = request.IsApproved;
        p.Notes = request.Notes;
        p.DecidedAt = DateTime.UtcNow;

        access.Audit(projectId, "TOPIC_DECIDED", $"{{\"decision\": \"{(request.IsApproved ? "Approved" : "Rejected")}\", \"notes\": \"{request.Notes?.Replace("\"", "\\\"")}\"}}");
        await access.Save(ct);
        logger.LogInformation("Topic decision persisted for project {ProjectId}", projectId);
        return NoContent();
    }

    [HttpGet("projects/{projectId:guid}/topic-history"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> GetTopicHistory(Guid projectId, CancellationToken ct)
    {
        var p = await access.Project(projectId, ct);
        await RequireTopicReviewer(p, ct);

        var events = await db.AuditEvents.AsNoTracking()
            .Where(x => x.ResourceId == projectId)
            .OrderByDescending(x => x.Timestamp)
            .Select(x => new
            {
                id = x.Id.ToString(),
                actorId = x.ActorId,
                actorName = x.ActorName,
                eventType = x.EventType,
                details = x.Details,
                timestamp = x.Timestamp
            })
            .ToListAsync(ct);

        var list = events.ToList();

        // Luôn đảm bảo có sự kiện đề xuất đề tài ban đầu trong lịch sử
        if (!list.Any(x => x.eventType == "TOPIC_PROPOSED"))
        {
            list.Add(new
            {
                id = $"proposal-{projectId}",
                actorId = Guid.Empty,
                actorName = p.AnalysisRun?.Group?.Name ?? "Nhóm sinh viên",
                eventType = "TOPIC_PROPOSED",
                details = p.AnalysisRun?.Group?.TopicName ?? p.AnalysisRun?.RepoName ?? "Đề xuất đề tài ban đầu",
                timestamp = p.AnalysisRun?.CreatedAt ?? DateTime.UtcNow.AddDays(-7)
            });
        }

        return Ok(list.OrderByDescending(x => x.timestamp));
    }

    private async Task RequireTopicReviewer(CourseProject project, CancellationToken ct)
    {
        if (project.AnalysisRun?.Group?.Class is Class courseClass)
            GroupTopicsController.RequireReviewer(await access.User(ct), courseClass);
        else
            await access.Staff(project, ct);
    }
}

[ApiController, Route("api/assessments/sessions"), Authorize]
public class AssessmentSessionsController(AppDbContext db, TeachingAccess access, ICurrentUserService current) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private async Task<AssessmentSession> Load(Guid id, CancellationToken ct)
    {
        var s = await db.AssessmentSessions.Include(x => x.Students).ThenInclude(x => x.Student).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Session unavailable.");
        await access.Project(s.ProjectId, ct);
        var user = await access.User(ct);
        if (user.Role == UserRole.Student && !s.Students.Any(x => x.StudentId == user.Id))
            throw new ForbiddenException();
        if (s.AssessmentRoundRevisionId.HasValue && user.Role == UserRole.Lecturer)
        {
            var allowed = await db.AssessmentRoundRevisions.AnyAsync(x =>
                x.Id == s.AssessmentRoundRevisionId && x.Round.Milestone.Class.LecturerId == user.Id, ct);
            if (!allowed)
                throw new ForbiddenException();
        }
        return s;
    }
    private object View(AssessmentSession s, CourseProject p, bool student)
    {
        var criteria = JsonSerializer.Deserialize<List<SessionCriterion>>(s.RubricJson, Json)!;
        var groupName = p.AnalysisRun?.Group?.Name ?? p.AnalysisRun?.RepoName ?? "";
        var courseCode = p.AnalysisRun?.Group?.Class?.Course?.Code ?? p.Course?.Code ?? "";
        var courseId = p.AnalysisRun?.Group?.Class?.CourseId ?? p.CourseId;
        return new
        {
            s.Id,
            s.Title,
            s.OralExamId,
            s.AssessmentRoundRevisionId,
            courseCode,
            groupName,
            s.Status,
            s.ScheduledStart,
            s.ScheduledEnd,
            s.ActualStartTime,
            s.ActualEndTime,
            rubric = new { id = s.Id, courseId, title = s.Title, criteria = criteria.Select(c => new { c.Id, c.Title, c.Description, c.MaxScore, c.Weight, levels = new object[0] }), totalScore = 100 },
            participants = s.Students.Select(x => new { user = new { id = x.Student.Id, email = x.Student.Email, fullName = x.Student.Email, role = "STUDENT", createdAt = x.Student.CreatedAt }, role = "CANDIDATE", isAudioActive = false, isVideoActive = false, isSpeaking = false, connectionQuality = "disconnected" }),
            oralQuestions = student || s.OralQuestionsJson is null
                ? null : JsonSerializer.Deserialize<List<OralQuestionSnapshot>>(s.OralQuestionsJson, Json),
            oralEvidence = student || s.OralQuestionsJson is null ? null : s.Students
                .Where(x => x.OralEvidenceJson != null)
                .ToDictionary(x => x.StudentId, x => JsonSerializer.Deserialize<List<OralEvidenceInput>>(x.OralEvidenceJson!, Json)),
            studentEvaluations = s.Students.Where(x => x.EvaluationJson != null &&
                (!student || (x.StudentId == current.UserId && (s.OralQuestionsJson == null || s.Status == "COMPLETED")))).ToDictionary(x => x.StudentId, x => JsonSerializer.Deserialize<List<ScoreInput>>(x.EvaluationJson!, Json))
        };
    }
    [ProducesResponseType(typeof(SessionSummaryView[]), 200)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default)
    {
        var u = await access.User(ct);
        return Ok(await db.AssessmentSessions.AsNoTracking().Where(s =>
            u.Role == UserRole.Admin ||
            (u.Role == UserRole.Lecturer && (s.AssessmentRoundRevision != null
                ? s.AssessmentRoundRevision.Round.Milestone.Class.LecturerId == u.Id
                : (s.Project.AnalysisRun.Group != null && s.Project.AnalysisRun.Group.Class.LecturerId == u.Id) ||
                  s.Project.Course.Classes.Any(cl => cl.LecturerId == u.Id))) ||
            (u.Role == UserRole.Student && s.Students.Any(x => x.StudentId == u.Id)))
            .OrderByDescending(s => s.ScheduledStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new { s.Id, s.Title, s.Status, s.ProjectId, s.ScheduledStart, s.ScheduledEnd, s.OralExamId, s.AssessmentRoundRevisionId })
            .ToListAsync(ct));
    }
    [ProducesResponseType(typeof(SessionView), 200)]
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) { var s = await Load(id, ct); return Ok(View(s, await access.Project(s.ProjectId, ct), (await access.User(ct)).Role == UserRole.Student)); }
    [HttpGet("round-options"), Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<IReadOnlyList<AssessmentRoundOption>>> RoundOptions(
        [FromQuery, Required] Guid? projectId, CancellationToken ct)
    {
        var project = await access.Project(projectId!.Value, ct);
        await access.Staff(project, ct);
        var courseClass = project.AnalysisRun.Group?.Class;
        if (courseClass == null)
            return Ok(Array.Empty<AssessmentRoundOption>());
        GroupTopicsController.RequireReviewer(await access.User(ct), courseClass);

        return Ok(await db.AssessmentRoundRevisions.AsNoTracking()
            .Where(x => x.Round.Milestone.ClassId == courseClass.Id &&
                !db.AssessmentRoundRevisions.Any(other => other.RoundId == x.RoundId && other.Version > x.Version))
            .OrderBy(x => x.OpensAt).ThenBy(x => x.RoundId)
            .Select(x => new AssessmentRoundOption(x.Id, x.RoundId, x.Version, x.Title, x.OpensAt, x.ClosesAt))
            .ToListAsync(ct));
    }

    [ProducesResponseType(typeof(ResourceCreated), 201)]
    [HttpPost, Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Create(CreateSessionRequest request, CancellationToken ct)
    {
        var p = await access.Project(request.ProjectId, ct); await access.Staff(p, ct);
        if (request.ScheduledStart.Kind != DateTimeKind.Utc || request.ScheduledEnd.Kind != DateTimeKind.Utc || request.ScheduledEnd <= request.ScheduledStart) return BadRequest(new { error = "Provide increasing UTC timestamps." });
        if (request.StudentIds.Distinct().Count() != request.StudentIds.Count) return BadRequest(new { error = "Student IDs must be unique." });
        
        var validStudentsCount = await db.Users.CountAsync(u => request.StudentIds.Contains(u.Id) &&
            (u.AnalysisRunId == request.ProjectId ||
             (p.AnalysisRun.GroupId != null && db.GroupMembers.Any(gm => gm.GroupId == p.AnalysisRun.GroupId && gm.StudentCode == u.StudentCode))) &&
            u.Role == UserRole.Student, ct);
        if (validStudentsCount != request.StudentIds.Count)
            return BadRequest(new { error = "Students must belong to this project." });

        var criteria = request.Criteria;
        if (request.AssessmentRoundRevisionId is Guid revisionId)
        {
            var courseClass = p.AnalysisRun.Group?.Class
                ?? throw new BadRequestException("Assign the project to a class before selecting an assessment round.");
            GroupTopicsController.RequireReviewer(await access.User(ct), courseClass);
            var revision = await db.AssessmentRoundRevisions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == revisionId && x.Round.Milestone.ClassId == courseClass.Id, ct)
                ?? throw new BadRequestException("Select a published assessment round revision from the project's class.");
            if (request.ScheduledStart < revision.OpensAt || request.ScheduledEnd > revision.ClosesAt)
                throw new BadRequestException("Schedule the session within the assessment round's published window.");

            criteria = JsonSerializer.Deserialize<AssessmentRoundConfiguration>(revision.ConfigurationJson)!.Rubric;
        }
        if (criteria is null || criteria.Any(c => c is null) ||
            criteria.Select(c => c.Id).Distinct().Count() != criteria.Count || criteria.Sum(c => c.Weight) != 100)
            return BadRequest(new { error = "Use distinct criteria with weights totaling 100." });
        if (request.OralExamId is Guid examId)
            await LoadOralQuestions(examId, p, ct);

        if (db.Entry(p).State == EntityState.Detached) db.CourseProjects.Add(p);
        var s = new AssessmentSession
        {
            ProjectId = request.ProjectId,
            OralExamId = request.OralExamId,
            AssessmentRoundRevisionId = request.AssessmentRoundRevisionId,
            Title = request.Title,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = request.ScheduledEnd,
            RubricJson = JsonSerializer.Serialize(criteria, Json),
            Students = request.StudentIds.Select(id => new SessionStudent { StudentId = id }).ToList()
        };
        db.AssessmentSessions.Add(s); access.Audit(s.Id, "SESSION_CREATED", s.Title); await access.Save(ct); return CreatedAtAction(nameof(Get), new { id = s.Id }, new { s.Id });
    }
    [ProducesResponseType(typeof(SessionStartView), 200)]
    [HttpPost("{id:guid}/start"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct);
        var project = await access.Project(s.ProjectId, ct);
        await access.Staff(project, ct);
        SessionRules.Require(s.Status, "CREATED");

        if (s.OralExamId is Guid examId)
            s.OralQuestionsJson = JsonSerializer.Serialize(await LoadOralQuestions(examId, project, ct), Json);

        s.Status = "IN_PROGRESS";
        s.ActualStartTime = DateTime.UtcNow;
        s.Version = Guid.NewGuid();
        access.Audit(id, "SESSION_START", s.Title);
        await access.Save(ct);
        return Ok(new { s.Id, s.Status, s.ActualStartTime });
    }

    private async Task<List<OralQuestionSnapshot>> LoadOralQuestions(Guid examId, CourseProject project, CancellationToken ct)
    {
        var classId = project.AnalysisRun.Group?.ClassId
            ?? throw new BadRequestException("Assign the project to a class before using an oral exam.");
        var questions = await db.OralExams.AsNoTracking().Where(exam => exam.Id == examId && exam.ClassId == classId)
            .Select(exam => exam.Questions.OrderBy(question => question.Id)
                .Select(question => new OralQuestionSnapshot(question.Id, question.Content, question.ReferenceContext)).ToList())
            .SingleOrDefaultAsync(ct);
        if (questions is null || questions.Count == 0)
            throw new BadRequestException("Select a non-empty oral exam from the project's class.");
        return questions;
    }

    [HttpPut("{id:guid}/students/{studentId:guid}/oral-evidence"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> SaveOralEvidence(Guid id, Guid studentId, SaveOralEvidenceRequest request, CancellationToken ct)
    {
        var session = await Load(id, ct);
        await access.Staff(await access.Project(session.ProjectId, ct), ct);
        SessionRules.Require(session.Status, "IN_PROGRESS");
        var student = session.Students.SingleOrDefault(x => x.StudentId == studentId)
            ?? throw new NotFoundException("Student not assigned to session.");
        if (session.OralQuestionsJson is null)
            throw new ConflictException("The session has no oral exam snapshot.");

        var questionIds = JsonSerializer.Deserialize<List<OralQuestionSnapshot>>(session.OralQuestionsJson, Json)!
            .Select(question => question.Id).ToHashSet();
        if (request.Answers.Any(answer => answer is null || !questionIds.Contains(answer.QuestionId))
            || request.Answers.Select(answer => answer.QuestionId).Distinct().Count() != request.Answers.Count)
            throw new BadRequestException("Provide one entry per question asked from this session's oral exam.");

        var evidenceJson = JsonSerializer.Serialize(request.Answers, Json);
        if (student.OralEvidenceJson == evidenceJson) return NoContent();

        student.OralEvidenceJson = evidenceJson;
        // Scores based on earlier evidence must be reviewed before publication.
        student.EvaluationJson = null;
        student.EvaluatorId = null;
        student.EvaluatedAt = null;
        session.Version = Guid.NewGuid();
        access.Audit(id, "ORAL_EVIDENCE_RECORDED", studentId.ToString());
        await access.Save(ct);
        return NoContent();
    }
    [HttpPost("{id:guid}/students/{studentId:guid}/evaluate"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Evaluate(Guid id, Guid studentId, EvaluateRequest request, CancellationToken ct)
    {
        var s = await Load(id, ct); await access.Staff(await access.Project(s.ProjectId, ct), ct); SessionRules.Require(s.Status, "IN_PROGRESS"); var student = s.Students.SingleOrDefault(x => x.StudentId == studentId) ?? throw new NotFoundException("Student not assigned to session.");
        if (s.OralQuestionsJson is not null && student.OralEvidenceJson is null)
            throw new ConflictException("Record this student's oral evidence before scoring.");
        SessionRules.ValidateScores(JsonSerializer.Deserialize<List<SessionCriterion>>(s.RubricJson, Json)!, request.Evaluations);
        student.EvaluationJson = JsonSerializer.Serialize(request.Evaluations, Json); student.EvaluatorId = current.UserId; student.EvaluatedAt = DateTime.UtcNow; s.Version = Guid.NewGuid(); access.Audit(id, "SCORE_RECORDED", studentId.ToString()); await access.Save(ct); return NoContent();
    }
    [ProducesResponseType(typeof(SessionView), 200)]
    [HttpPost("{id:guid}/conclude"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Conclude(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct); var p = await access.Project(s.ProjectId, ct); await access.Staff(p, ct); SessionRules.Require(s.Status, "IN_PROGRESS"); if (s.Students.Any(x => x.EvaluationJson == null)) return Conflict(new { error = "Evaluate every assigned student before concluding." });
        s.Status = "COMPLETED"; s.ActualEndTime = DateTime.UtcNow; s.Version = Guid.NewGuid(); access.Audit(id, "SESSION_CONCLUDED", s.Title); await access.Save(ct); return Ok(View(s, p, false));
    }
}
