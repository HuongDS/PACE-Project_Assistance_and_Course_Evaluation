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

public record CreateCourseRequest([Required, MaxLength(40)] string Code, [Required, MaxLength(255)] string Name, [Required, MaxLength(40)] string Semester, Guid LecturerId);
public record TopicDecision(bool IsApproved, [MaxLength(4000)] string? Notes);
public record SessionCriterion(Guid Id, [Required, MaxLength(255)] string Title, [MaxLength(4000)] string Description, [Range(0.01, 1000)] decimal MaxScore, [Range(0.01, 100)] decimal Weight);
public record CreateSessionRequest(Guid ProjectId, [Required, MaxLength(255)] string Title, DateTime ScheduledStart, DateTime ScheduledEnd, [Required, MinLength(1), MaxLength(100)] List<Guid> StudentIds, [Required, MinLength(1), MaxLength(100)] List<SessionCriterion> Criteria, Guid? OralExamId = null);
public record ScoreInput(Guid CriterionId, [Range(0, 1000)] decimal Score, [MaxLength(4000)] string? Feedback);
public record EvaluateRequest([Required, MinLength(1), MaxLength(100)] List<ScoreInput> Evaluations);

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
        }
        else if (run != null)
        {
            p.AnalysisRun = run;
            if (p.Course == null && course != null)
            {
                p.Course = course;
                p.CourseId = course.Id;
            }
        }

        var u = await User(ct);
        bool isAdmin = u.Role == UserRole.Admin;
        bool isLecturer = u.Role == UserRole.Lecturer && (
            (p.Course != null && p.Course.LecturerId == u.Id) ||
            (run?.Group?.Class?.Course != null && run.Group.Class.Course.LecturerId == u.Id)
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
        var u = await User(ct); if (u.Role != UserRole.Admin && !(u.Role == UserRole.Lecturer && p.Course.LecturerId == u.Id)) throw new ForbiddenException();
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
    private async Task<IQueryable<Course>> Visible(CancellationToken ct)
    {
        var u = await access.User(ct);
        return db.Courses.AsNoTracking().Where(c =>
            u.Role == UserRole.Admin ||
            (u.Role == UserRole.Lecturer && c.LecturerId == u.Id) ||
            (u.Role == UserRole.Student && (
                c.Projects.Any(p => p.AnalysisRunId == u.AnalysisRunId) ||
                c.Classes.Any(cl => cl.Groups.Any(g =>
                    g.AnalysisRuns.Any(r => r.Id == u.AnalysisRunId) ||
                    g.Members.Any(m => m.StudentCode == u.StudentCode))))));
    }
    private static IQueryable<object> Details(IQueryable<Course> q) => q.Select(c => new
    {
        c.Id,
        c.Code,
        c.Name,
        c.Semester,
        c.LecturerId,
        lecturerName = c.Lecturer.Email,
        totalGroups = c.Classes.SelectMany(cl => cl.Groups).Count() > 0
            ? c.Classes.SelectMany(cl => cl.Groups).Count()
            : c.Projects.Count,
        milestones = new object[0],
        projects = c.Classes.SelectMany(cl => cl.Groups).Any(g => g.AnalysisRuns.Any())
            ? c.Classes.SelectMany(cl => cl.Groups)
                .SelectMany(g => g.AnalysisRuns.Select(r => new
                {
                    id = r.Id,
                    name = r.RepoName ?? g.Name,
                    isApproved = (bool?)true,
                    notes = (string?)null,
                    decidedAt = (DateTime?)r.CreatedAt,
                    students = r.Users.Where(u => u.Role == UserRole.Student).Select(u => new { u.Id, u.Email })
                }))
            : c.Projects.Select(p => new
            {
                id = p.AnalysisRunId,
                name = p.AnalysisRun.RepoName ?? "",
                isApproved = p.IsApproved,
                notes = p.Notes,
                decidedAt = p.DecidedAt,
                students = p.AnalysisRun.Users.Where(u => u.Role == UserRole.Student).Select(u => new { u.Id, u.Email })
            })
    });
    [ProducesResponseType(typeof(CourseView[]), 200)]
    [HttpGet] public async Task<IActionResult> List([FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default) => Ok(await Details((await Visible(ct)).OrderBy(c => c.Code).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct));
    [ProducesResponseType(typeof(CourseView), 200)]
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok(await Details((await Visible(ct)).Where(c => c.Id == id)).SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Course unavailable."));
    [ProducesResponseType(typeof(ResourceCreated), 201)]
    [HttpPost, Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create(CreateCourseRequest request, CancellationToken ct)
    {
        if (!await db.Users.AnyAsync(u => u.Id == request.LecturerId && u.Role == UserRole.Lecturer, ct)) return BadRequest(new { error = "Assign an existing Lecturer account." });
        if (await db.Courses.AnyAsync(c => c.Code == request.Code && c.Semester == request.Semester && c.LecturerId == request.LecturerId, ct)) return Conflict(new { error = "Course already exists." });
        var c = new Course { Code = request.Code, Name = request.Name, Semester = request.Semester, LecturerId = request.LecturerId }; db.Courses.Add(c); access.Audit(c.Id, "COURSE_CREATED", c.Code); await access.Save(ct); return CreatedAtAction(nameof(Get), new { id = c.Id }, new { c.Id });
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
        var p = await access.Project(projectId, ct); await access.Staff(p, ct); p.IsApproved = request.IsApproved; p.Notes = request.Notes; p.DecidedAt = DateTime.UtcNow;
        access.Audit(projectId, "TOPIC_DECIDED", request.IsApproved ? "Approved" : "Rejected"); await access.Save(ct); logger.LogInformation("Topic decision persisted for project {ProjectId}", projectId); return NoContent();
    }
}

[ApiController, Route("api/assessments/sessions"), Authorize]
public class AssessmentSessionsController(AppDbContext db, TeachingAccess access, ICurrentUserService current) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private async Task<AssessmentSession> Load(Guid id, CancellationToken ct)
    {
        var s = await db.AssessmentSessions.Include(x => x.Students).ThenInclude(x => x.Student).SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Session unavailable.");
        await access.Project(s.ProjectId, ct); var u = await access.User(ct); if (u.Role == UserRole.Student && !s.Students.Any(x => x.StudentId == u.Id)) throw new ForbiddenException(); return s;
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
            courseCode,
            groupName,
            s.Status,
            s.ScheduledStart,
            s.ScheduledEnd,
            s.ActualStartTime,
            s.ActualEndTime,
            rubric = new { id = s.Id, courseId, title = s.Title, criteria = criteria.Select(c => new { c.Id, c.Title, c.Description, c.MaxScore, c.Weight, levels = new object[0] }), totalScore = 100 },
            participants = s.Students.Select(x => new { user = new { id = x.Student.Id, email = x.Student.Email, fullName = x.Student.Email, role = "STUDENT", createdAt = x.Student.CreatedAt }, role = "CANDIDATE", isAudioActive = false, isVideoActive = false, isSpeaking = false, connectionQuality = "disconnected" }),
            studentEvaluations = s.Students.Where(x => x.EvaluationJson != null && (!student || x.StudentId == current.UserId)).ToDictionary(x => x.StudentId, x => JsonSerializer.Deserialize<List<ScoreInput>>(x.EvaluationJson!, Json))
        };
    }
    [ProducesResponseType(typeof(SessionSummaryView[]), 200)]
    [HttpGet]
    public async Task<IActionResult> List([FromQuery, Range(1, int.MaxValue)] int page = 1, [FromQuery, Range(1, 100)] int pageSize = 20, CancellationToken ct = default)
    {
        var u = await access.User(ct);
        return Ok(await db.AssessmentSessions.AsNoTracking().Where(s =>
            u.Role == UserRole.Admin ||
            (u.Role == UserRole.Lecturer && (s.Project.Course.LecturerId == u.Id || (s.Project.AnalysisRun.Group != null && s.Project.AnalysisRun.Group.Class.Course.LecturerId == u.Id))) ||
            (u.Role == UserRole.Student && s.Students.Any(x => x.StudentId == u.Id)))
            .OrderByDescending(s => s.ScheduledStart)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new { s.Id, s.Title, s.Status, s.ProjectId, s.ScheduledStart, s.ScheduledEnd, s.OralExamId })
            .ToListAsync(ct));
    }
    [ProducesResponseType(typeof(SessionView), 200)]
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) { var s = await Load(id, ct); return Ok(View(s, await access.Project(s.ProjectId, ct), (await access.User(ct)).Role == UserRole.Student)); }
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

        if (request.Criteria.Select(c => c.Id).Distinct().Count() != request.Criteria.Count || request.Criteria.Sum(c => c.Weight) != 100) return BadRequest(new { error = "Use distinct criteria with weights totaling 100." });
        var s = new AssessmentSession
        {
            ProjectId = request.ProjectId,
            OralExamId = request.OralExamId,
            Title = request.Title,
            ScheduledStart = request.ScheduledStart,
            ScheduledEnd = request.ScheduledEnd,
            RubricJson = JsonSerializer.Serialize(request.Criteria, Json),
            Students = request.StudentIds.Select(id => new SessionStudent { StudentId = id }).ToList()
        };
        db.AssessmentSessions.Add(s); access.Audit(s.Id, "SESSION_CREATED", s.Title); await access.Save(ct); return CreatedAtAction(nameof(Get), new { id = s.Id }, new { s.Id });
    }
    [ProducesResponseType(typeof(SessionStartView), 200)]
    [HttpPost("{id:guid}/start"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        var s = await Load(id, ct); await access.Staff(await access.Project(s.ProjectId, ct), ct); SessionRules.Require(s.Status, "CREATED"); s.Status = "IN_PROGRESS"; s.ActualStartTime = DateTime.UtcNow; s.Version = Guid.NewGuid(); access.Audit(id, "SESSION_START", s.Title); await access.Save(ct); return Ok(new { s.Id, s.Status, s.ActualStartTime });
    }
    [HttpPost("{id:guid}/students/{studentId:guid}/evaluate"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Evaluate(Guid id, Guid studentId, EvaluateRequest request, CancellationToken ct)
    {
        var s = await Load(id, ct); await access.Staff(await access.Project(s.ProjectId, ct), ct); SessionRules.Require(s.Status, "IN_PROGRESS"); var student = s.Students.SingleOrDefault(x => x.StudentId == studentId) ?? throw new NotFoundException("Student not assigned to session.");
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
