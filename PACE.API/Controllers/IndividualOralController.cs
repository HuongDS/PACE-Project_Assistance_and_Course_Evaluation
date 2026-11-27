using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Exceptions;
using PACE.Application.Oral;
using PACE.Application.Common.Interfaces;
using PACE.Domain.Entities;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record IndividualOralOptions([Range(5, 180)] int DurationMinutes = 20, [Range(1, 10)] int QuestionsPerStudent = 4,
    [MaxLength(20)] string? Difficulty = null, DateTime? CodeDeadline = null);
public record ScanCodeRequest(DateTime? CodeDeadline = null);
public record AuthorChoice([Required, MaxLength(320)] string Key, Guid? StudentId);
public record SaveAuthorsRequest([Required, MaxLength(200)] List<AuthorChoice> Authors);
/// <param name="Mode">replace: làm lại cả đề; add: thêm câu; swap: thay một câu.</param>
public record GenerateStudentQuestionsRequest([MaxLength(2000)] string? Instructions = null, [RegularExpression("replace|add|swap")] string Mode = "replace",
    Guid? QuestionId = null, [Range(1, 10)] int? Count = null);
public record OralQuestionEdit(Guid? Id, [Required, MaxLength(4000)] string Content, [MaxLength(4000)] string? SuggestedAnswer = null,
    [MaxLength(1000)] string? MethodKey = null, Guid? CriterionId = null);
public record SaveStudentQuestionsRequest([Required, MaxLength(20)] List<OralQuestionEdit> Questions, bool Confirmed);
public record SaveOralAnswerRequest(Guid QuestionId, [Required(AllowEmptyStrings = true), MaxLength(8000)] string Text);
/// <summary>Cho biết nhóm (khi tạo phiên) hoặc lớp (khi soạn đợt đánh giá). Phần soạn dở của đợt được gửi kèm để AI bám theo.</summary>
public record RubricSuggestionRequest(Guid? ProjectId = null, Guid? ClassId = null, Guid? RoundRevisionId = null,
    [MaxLength(255)] string? RoundTitle = null, [MaxLength(100)] List<string>? Deliverables = null, [MaxLength(100)] List<string>? Outcomes = null,
    [MaxLength(100)] List<string>? Topics = null, [MaxLength(8000)] string? GradingGuide = null,
    [MaxLength(100)] List<SessionCriterion>? Current = null, [MaxLength(2000)] string? Notes = null);

/// <summary>
/// Phiên vấn đáp mở trong một khoảng thời gian: giảng viên chụp mã tại mốc hạn, duyệt đề riêng của từng sinh viên;
/// sinh viên tự vào làm; AI chỉ viết nhận xét theo rubric, điểm do giảng viên cho qua phần chấm sẵn có.
/// </summary>
[ApiController, Route("api/assessments/sessions/{id:guid}/individual"), Authorize]
public class IndividualOralController(AppDbContext db, TeachingAccess access, ICurrentUserService current,
    ICodeSnapshotReader reader, IOralAssistant assistant, TimeProvider clock) : ControllerBase
{
    /// <summary>Câu trả lời gửi đi đúng lúc hết giờ vẫn được nhận trong khoảng này.</summary>
    private static readonly TimeSpan Grace = TimeSpan.FromSeconds(30);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    private static T Read<T>(string? json) where T : new() => json is null ? new T() : JsonSerializer.Deserialize<T>(json, Json)!;
    private static string Write<T>(T value) => JsonSerializer.Serialize(value, Json);
    private static CodeSnapshot? Snapshot(IndividualOralSetup setup)
        => setup.SnapshotJson is null ? null : JsonSerializer.Deserialize<CodeSnapshot>(setup.SnapshotJson, Json);

    private async Task<(AssessmentSession Session, CourseProject Project)> Load(Guid id, CancellationToken ct)
    {
        var session = await db.AssessmentSessions.Include(s => s.Students).ThenInclude(s => s.Student)
            .Include(s => s.IndividualSetup).SingleOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Session unavailable.");
        if (!session.IsIndividual || session.IndividualSetup is null)
            throw new NotFoundException("Phiên này không phải vấn đáp riêng từng sinh viên.");
        return (session, await access.Project(session.ProjectId, ct));
    }

    private async Task<(AssessmentSession Session, CourseProject Project)> LoadStaff(Guid id, CancellationToken ct)
    {
        var loaded = await Load(id, ct);
        await access.Staff(loaded.Project, ct);
        var user = await access.User(ct);
        if (loaded.Session.AssessmentRoundRevisionId.HasValue && user.Role == UserRole.Lecturer
            && !await db.AssessmentRoundRevisions.AnyAsync(r => r.Id == loaded.Session.AssessmentRoundRevisionId && r.Round.Milestone.Class.LecturerId == user.Id, ct))
            throw new ForbiddenException();
        return loaded;
    }

    private static void NotCompleted(AssessmentSession session)
    {
        if (session.Status == "COMPLETED") throw new ConflictException("Phiên đã kết thúc.");
    }

    private async Task<OralAttempt> Attempt(AssessmentSession session, Guid studentId, CancellationToken ct)
        => await db.OralAttempts.SingleOrDefaultAsync(a => a.SessionId == session.Id && a.StudentId == studentId, ct)
            ?? throw new NotFoundException("Sinh viên không thuộc phiên này.");

    /// <summary>Đã nộp, hoặc hết giờ thì coi như nộp với những gì đã lưu.</summary>
    public static DateTime? Finished(OralAttempt attempt, DateTime now)
        => attempt.SubmittedAt ?? (attempt.ExpiresAt is DateTime end && end + Grace < now ? end : null);

    private string State(OralAttempt attempt)
        => Finished(attempt, Now) != null ? (attempt.SubmittedAt != null ? "submitted" : "timedOut")
            : attempt.StartedAt != null ? "inProgress"
            : attempt.ConfirmedAt != null ? "ready" : "draft";

    // ----- Giảng viên -----

    [HttpGet, Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Overview(Guid id, CancellationToken ct)
    {
        var (session, project) = await LoadStaff(id, ct);
        return Ok(await View(session, project, ct));
    }

    private async Task<object> View(AssessmentSession session, CourseProject project, CancellationToken ct)
    {
        var setup = session.IndividualSetup!;
        var snapshot = Snapshot(setup);
        var attempts = await db.OralAttempts.Where(a => a.SessionId == session.Id).ToListAsync(ct);
        return new
        {
            sessionId = session.Id,
            session.Title,
            session.Status,
            groupName = project.AnalysisRun.Group?.Name ?? project.AnalysisRun.RepoName,
            repoUrl = project.AnalysisRun.RepoUrl,
            opensAt = session.ScheduledStart,
            closesAt = session.ScheduledEnd,
            setup.DurationMinutes,
            setup.QuestionsPerStudent,
            setup.Difficulty,
            setup.CodeDeadline,
            setup.CommitSha,
            setup.CommitAt,
            setup.ScannedAt,
            warnings = snapshot?.Warnings ?? [],
            authors = snapshot?.Authors ?? [],
            students = session.Students.OrderBy(s => s.Student.StudentCode, StringComparer.OrdinalIgnoreCase).Select(s =>
            {
                var attempt = attempts.Single(a => a.StudentId == s.StudentId);
                var owned = snapshot is null ? [] : OralOwnership.OwnedBy(snapshot, s.StudentId);
                return new
                {
                    studentId = s.StudentId,
                    studentCode = s.Student.StudentCode,
                    fullName = string.IsNullOrWhiteSpace(s.Student.FullName) ? s.Student.Email : s.Student.FullName,
                    state = State(attempt),
                    methodCount = owned.Count,
                    lineCount = owned.Sum(o => o.OwnLines.Count),
                    methods = owned.Take(40).Select(o => new { o.Method.Key, o.Method.Name, o.Method.File, o.Method.StartLine, lines = o.OwnLines.Count, share = (int)Math.Round(o.Share * 100) }),
                    questions = Read<List<OralQuestionItem>>(attempt.QuestionsJson),
                    attempt.ConfirmedAt,
                    attempt.StartedAt,
                    attempt.ExpiresAt,
                    submittedAt = Finished(attempt, Now),
                    answers = Read<List<OralAnswerItem>>(attempt.AnswersJson),
                    review = attempt.AiReviewJson is null ? null : JsonSerializer.Deserialize<OralReview>(attempt.AiReviewJson, Json),
                    attempt.AiReviewedAt,
                    evaluated = s.EvaluationJson != null
                };
            }).ToList()
        };
    }

    /// <summary>Đọc mã nguồn của nhóm tại mốc hạn và nhận ra ai viết hàm nào. Không đụng tới dữ liệu phân tích đã có.</summary>
    [HttpPost("scan"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Scan(Guid id, ScanCodeRequest request, CancellationToken ct)
    {
        var (session, project) = await LoadStaff(id, ct);
        NotCompleted(session);
        var repo = project.AnalysisRun.RepoUrl;
        if (string.IsNullOrWhiteSpace(repo)) throw new BadRequestException("Nhóm chưa gắn link repository.");
        var setup = session.IndividualSetup!;
        var deadline = request.CodeDeadline?.ToUniversalTime() ?? setup.CodeDeadline;

        var read = await reader.ReadAsync(repo, deadline, ct);
        var roster = session.Students.Select(s => new RosterStudent(s.StudentId, s.Student.StudentCode ?? "", s.Student.FullName ?? "", s.Student.Email)).ToList();
        // Tài khoản giảng viên đã gán tay thì giữ, kể cả lựa chọn ở phiên trước của cùng nhóm.
        var earlier = await db.IndividualOralSetups.AsNoTracking()
            .Where(x => x.SessionId != id && x.Session.ProjectId == session.ProjectId && x.SnapshotJson != null)
            .OrderByDescending(x => x.ScannedAt).Take(5).Select(x => x.SnapshotJson!).ToListAsync(ct);
        var chosen = new Dictionary<string, SnapshotAuthor>(StringComparer.Ordinal);
        var members = roster.Select(r => r.Id).ToHashSet();
        foreach (var author in new[] { setup.SnapshotJson }.Concat(earlier).Where(json => json != null)
                     .SelectMany(json => JsonSerializer.Deserialize<CodeSnapshot>(json!, Json)!.Authors))
            if (author.MatchedBy == ManualChoice && (author.StudentId is null || members.Contains(author.StudentId.Value)))
                chosen.TryAdd(author.Key, author);
        var authors = OralOwnership.Match(read.Snapshot.Authors, roster)
            .Select(a => chosen.TryGetValue(a.Key, out var kept) ? a with { StudentId = kept.StudentId, MatchedBy = kept.MatchedBy } : a).ToList();

        setup.CodeDeadline = deadline;
        setup.CommitSha = read.Revision.Sha;
        setup.CommitAt = read.Revision.CommittedAt;
        setup.ScannedAt = Now;
        setup.SnapshotJson = Write(read.Snapshot with { Authors = authors });
        session.Version = Guid.NewGuid();
        access.Audit(id, "ORAL_CODE_SCANNED", $"{read.Revision.Sha} · {read.Snapshot.Methods.Count} hàm");
        await access.Save(ct);
        return Ok(await View(session, project, ct));
    }

    private const string ManualChoice = "giảng viên chọn";

    [HttpPut("authors"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> SaveAuthors(Guid id, SaveAuthorsRequest request, CancellationToken ct)
    {
        var (session, project) = await LoadStaff(id, ct);
        NotCompleted(session);
        var setup = session.IndividualSetup!;
        var snapshot = Snapshot(setup) ?? throw new ConflictException("Chưa đọc mã nguồn.");
        var students = session.Students.Select(s => s.StudentId).ToHashSet();
        if (request.Authors.Any(a => a is null || (a.StudentId.HasValue && !students.Contains(a.StudentId.Value))
                || snapshot.Authors.All(x => x.Key != a.Key)))
            throw new BadRequestException("Chỉ gán được tài khoản trong mã nguồn cho sinh viên của phiên.");
        var choices = request.Authors.GroupBy(a => a.Key).ToDictionary(g => g.Key, g => g.Last().StudentId);
        setup.SnapshotJson = Write(snapshot with
        {
            Authors = snapshot.Authors.Select(a => choices.TryGetValue(a.Key, out var student) && student != a.StudentId
                ? a with { StudentId = student, MatchedBy = ManualChoice } : a).ToList()
        });
        session.Version = Guid.NewGuid();
        access.Audit(id, "ORAL_AUTHORS_ASSIGNED", $"{choices.Count} tài khoản git");
        await access.Save(ct);
        return Ok(await View(session, project, ct));
    }

    private static List<RubricLineInput> Lines(IEnumerable<SessionCriterion> rubric)
        => rubric.Select(c => new RubricLineInput(c.Id, c.Title, c.Description, c.Weight, c.Levels)).ToList();

    [HttpPost("students/{studentId:guid}/generate"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Generate(Guid id, Guid studentId, GenerateStudentQuestionsRequest request, CancellationToken ct)
    {
        var (session, project) = await LoadStaff(id, ct);
        NotCompleted(session);
        var attempt = await Attempt(session, studentId, ct);
        if (attempt.StartedAt != null) throw new ConflictException("Sinh viên đã vào thi, không đổi đề được. Mở lại lượt trước nếu cần.");
        var setup = session.IndividualSetup!;
        var snapshot = Snapshot(setup) ?? throw new ConflictException("Hãy đọc mã nguồn trước khi sinh câu hỏi.");
        if (snapshot.Methods.Any(m => m.LineOwners is null))
            throw new ConflictException("Bản đọc mã này đã cũ, chưa ghi ai viết từng dòng. Hãy bấm đọc lại mã nguồn.");

        var current = Read<List<OralQuestionItem>>(attempt.QuestionsJson);
        var swapped = request.Mode == "swap" ? current.FindIndex(q => q.Id == request.QuestionId) : -1;
        if (request.Mode == "swap" && swapped < 0) throw new BadRequestException("Câu cần thay không có trong đề.");
        var count = request.Mode switch { "swap" => 1, "add" => request.Count ?? 1, _ => request.Count ?? setup.QuestionsPerStudent };
        if (request.Mode == "add" && current.Count + count > 20) throw new BadRequestException("Một đề tối đa 20 câu.");
        var kept = request.Mode == "replace" ? [] : current;

        // Thêm hoặc thay câu thì ưu tiên hàm chưa được hỏi.
        var asked = kept.Where(q => q.MethodKey != null).Select(q => q.MethodKey!).ToHashSet();
        var methods = OralOwnership.ForPrompt(OralOwnership.OwnedBy(snapshot, studentId), asked);

        var instructions = request.Instructions;
        if (session.AssessmentRoundRevisionId is Guid revisionId)
        {
            var configuration = await db.AssessmentRoundRevisions.AsNoTracking().Where(r => r.Id == revisionId).Select(r => r.ConfigurationJson).SingleAsync(ct);
            var guide = JsonSerializer.Deserialize<AssessmentRoundConfiguration>(configuration)?.AiQuestioningInstructions;
            instructions = string.Join('\n', new[] { guide, instructions }.Where(x => !string.IsNullOrWhiteSpace(x)));
        }

        var batch = await assistant.GenerateQuestionsAsync(new QuestionRequest(
            project.AnalysisRun.Group?.TopicName ?? project.AnalysisRun.RepoName ?? "phần mềm", methods, count, setup.Difficulty, instructions,
            Lines(JsonSerializer.Deserialize<List<SessionCriterion>>(session.RubricJson, Json)!), kept.Select(q => q.Content).ToList(),
            kept.Where(q => q.MethodKey != null && q.FromLine != null && q.ToLine != null).Select(q => new AskedRange(q.MethodKey!, q.FromLine!.Value, q.ToLine!.Value)).ToList(),
            // Khi đổi một câu thì tiêu chí của câu bị thay coi như còn trống.
            kept.Where((q, i) => i != swapped && q.CriterionId != null).Select(q => q.CriterionId!.Value).ToList()), ct);

        var questions = request.Mode switch
        {
            "add" => [.. current, .. batch.Questions],
            "swap" => current.Select((q, i) => i == swapped ? batch.Questions[0] : q).ToList(),
            _ => batch.Questions
        };
        attempt.QuestionsJson = Write(questions);
        attempt.ConfirmedAt = null;
        attempt.Version = Guid.NewGuid();
        access.Audit(id, "ORAL_QUESTIONS_GENERATED", $"{studentId} · {request.Mode}");
        await access.Save(ct);
        return Ok(new { questions, batch.Note });
    }

    [HttpPut("students/{studentId:guid}/questions"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> SaveQuestions(Guid id, Guid studentId, SaveStudentQuestionsRequest request, CancellationToken ct)
    {
        var (session, _) = await LoadStaff(id, ct);
        NotCompleted(session);
        var attempt = await Attempt(session, studentId, ct);
        if (attempt.StartedAt != null) throw new ConflictException("Sinh viên đã vào thi, không đổi đề được. Mở lại lượt trước nếu cần.");
        if (request.Questions.Any(q => q is null || string.IsNullOrWhiteSpace(q.Content))) throw new BadRequestException("Câu hỏi không được để trống.");
        if (request.Confirmed && request.Questions.Count == 0) throw new BadRequestException("Cần ít nhất một câu hỏi để chốt đề.");
        var criteria = JsonSerializer.Deserialize<List<SessionCriterion>>(session.RubricJson, Json)!.Select(c => c.Id).ToHashSet();

        var old = Read<List<OralQuestionItem>>(attempt.QuestionsJson).ToDictionary(q => q.Id);
        var snapshot = Snapshot(session.IndividualSetup!);
        var owned = snapshot is null ? [] : OralOwnership.OwnedBy(snapshot, studentId);
        var questions = request.Questions.Select(edit =>
        {
            var question = (edit.Id is Guid existing ? old.GetValueOrDefault(existing) : null)
                ?? new OralQuestionItem(Guid.NewGuid(), "", null, null, null, null, null);
            // Giảng viên gắn một hàm cho câu tự viết: chỉ nhận hàm đứng tên sinh viên này.
            if (edit.MethodKey != null && edit.MethodKey != question.MethodKey)
            {
                var mine = owned.FirstOrDefault(o => o.Method.Key == edit.MethodKey)
                    ?? throw new BadRequestException("Hàm được chọn không đứng tên sinh viên này.");
                question = question with
                {
                    File = mine.Method.File, Method = mine.Method.Name, StartLine = mine.Method.StartLine, Code = mine.Method.Code, MethodKey = mine.Method.Key,
                    OwnLines = mine.OwnLines, FromLine = mine.OwnLines[0], ToLine = mine.OwnLines[^1],
                    Commits = mine.OwnCommits.Take(4).Select(c => new QuestionCommit(c.Sha, c.Message, c.At)).ToList()
                };
            }
            return question with
            {
                Content = edit.Content.Trim(),
                SuggestedAnswer = string.IsNullOrWhiteSpace(edit.SuggestedAnswer) ? null : edit.SuggestedAnswer.Trim(),
                CriterionId = edit.CriterionId is Guid criterion && criteria.Contains(criterion) ? criterion : null
            };
        }).ToList();
        if (questions.Select(q => q.Id).Distinct().Count() != questions.Count) throw new BadRequestException("Câu hỏi bị lặp.");

        attempt.QuestionsJson = Write(questions);
        attempt.ConfirmedAt = request.Confirmed ? Now : null;
        attempt.Version = Guid.NewGuid();
        access.Audit(id, request.Confirmed ? "ORAL_QUESTIONS_CONFIRMED" : "ORAL_QUESTIONS_EDITED", studentId.ToString());
        await access.Save(ct);
        return Ok(questions);
    }

    /// <summary>Cho một sinh viên thi lại. Lượt cũ được giữ trong lịch sử; điểm đã chấm bị bỏ.</summary>
    [HttpPost("students/{studentId:guid}/reopen"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Reopen(Guid id, Guid studentId, CancellationToken ct)
    {
        var (session, _) = await LoadStaff(id, ct);
        SessionRules.Require(session.Status, "IN_PROGRESS");
        var attempt = await Attempt(session, studentId, ct);
        if (attempt.StartedAt == null) throw new ConflictException("Sinh viên chưa thi.");

        var history = Read<List<JsonElement>>(attempt.HistoryJson);
        history.Add(JsonSerializer.SerializeToElement(new { attempt.StartedAt, attempt.SubmittedAt, answers = Read<List<OralAnswerItem>>(attempt.AnswersJson), reopenedAt = Now }, Json));
        attempt.HistoryJson = Write(history);
        attempt.StartedAt = attempt.ExpiresAt = attempt.SubmittedAt = attempt.AiReviewedAt = null;
        attempt.AnswersJson = "[]";
        attempt.AiReviewJson = null;
        attempt.Version = Guid.NewGuid();
        var student = session.Students.Single(s => s.StudentId == studentId);
        student.EvaluationJson = null; student.EvaluatorId = null; student.EvaluatedAt = null;
        session.Version = Guid.NewGuid();
        access.Audit(id, "ORAL_ATTEMPT_REOPENED", studentId.ToString());
        await access.Save(ct);
        return NoContent();
    }

    [HttpPost("students/{studentId:guid}/review"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> Review(Guid id, Guid studentId, CancellationToken ct)
    {
        var (session, _) = await LoadStaff(id, ct);
        NotCompleted(session);
        var attempt = await Attempt(session, studentId, ct);
        if (Finished(attempt, Now) == null) throw new ConflictException("Sinh viên chưa làm xong bài.");
        var rubric = Lines(JsonSerializer.Deserialize<List<SessionCriterion>>(session.RubricJson, Json)!);
        var review = await assistant.ReviewAsync(rubric, Read<List<OralQuestionItem>>(attempt.QuestionsJson), Read<List<OralAnswerItem>>(attempt.AnswersJson), ct);
        attempt.AiReviewJson = Write(review);
        attempt.AiReviewedAt = Now;
        attempt.Version = Guid.NewGuid();
        access.Audit(id, "ORAL_AI_REVIEW", studentId.ToString());
        await access.Save(ct);
        return Ok(review);
    }

    // ----- Sinh viên -----

    private async Task<(AssessmentSession Session, CourseProject Project, OralAttempt Attempt)> LoadMine(Guid id, CancellationToken ct)
    {
        var (session, project) = await Load(id, ct);
        if (session.Students.All(s => s.StudentId != current.UserId)) throw new ForbiddenException();
        return (session, project, await Attempt(session, current.UserId, ct));
    }

    private object Mine(AssessmentSession session, CourseProject project, OralAttempt attempt)
    {
        var finished = Finished(attempt, Now);
        var started = attempt.StartedAt != null;
        var questions = Read<List<OralQuestionItem>>(attempt.QuestionsJson);
        var state = finished != null ? "done"
            : session.Status == "COMPLETED" || Now > session.ScheduledEnd ? "closed"
            : started ? "inProgress"
            : session.Status != "IN_PROGRESS" || attempt.ConfirmedAt == null ? "notReady"
            : Now < session.ScheduledStart ? "upcoming" : "open";
        return new
        {
            sessionId = session.Id,
            session.Title,
            groupName = project.AnalysisRun.Group?.Name ?? project.AnalysisRun.RepoName,
            courseCode = project.AnalysisRun.Group?.Class?.Course?.Code ?? project.Course?.Code,
            state,
            opensAt = session.ScheduledStart,
            closesAt = session.ScheduledEnd,
            session.IndividualSetup!.DurationMinutes,
            questionCount = attempt.ConfirmedAt == null ? session.IndividualSetup.QuestionsPerStudent : questions.Count,
            attempt.StartedAt,
            attempt.ExpiresAt,
            submittedAt = finished,
            serverNow = Now,
            // Đề chỉ hiện sau khi bấm vào thi; đáp án gợi ý không bao giờ gửi cho sinh viên.
            questions = started ? questions.Select(q => new { q.Id, q.Content, q.Method, q.File, q.StartLine, q.Code, q.FromLine, q.ToLine, q.OwnLines }) : null,
            answers = Read<List<OralAnswerItem>>(attempt.AnswersJson)
        };
    }

    [HttpGet("me"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Me(Guid id, CancellationToken ct)
    {
        var (session, project, attempt) = await LoadMine(id, ct);
        return Ok(Mine(session, project, attempt));
    }

    [HttpPost("me/join"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Join(Guid id, CancellationToken ct)
    {
        var (session, project, attempt) = await LoadMine(id, ct);
        if (session.Status != "IN_PROGRESS") throw new ConflictException("Phiên chưa được giảng viên mở.");
        if (Finished(attempt, Now) != null) throw new ConflictException("Bạn đã làm xong lượt vấn đáp này.");
        if (Now < session.ScheduledStart) throw new ConflictException("Chưa đến giờ mở phiên.");
        if (Now >= session.ScheduledEnd) throw new ConflictException("Phiên đã hết thời gian.");
        if (attempt.ConfirmedAt == null) throw new ConflictException("Đề của bạn chưa sẵn sàng. Hãy báo giảng viên.");
        // Vào lại giữa chừng thì làm tiếp, đồng hồ không đặt lại.
        if (attempt.StartedAt == null)
        {
            attempt.StartedAt = Now;
            var end = Now.AddMinutes(session.IndividualSetup!.DurationMinutes);
            attempt.ExpiresAt = end < session.ScheduledEnd ? end : session.ScheduledEnd;
            attempt.Version = Guid.NewGuid();
            access.Audit(id, "ORAL_ATTEMPT_STARTED", current.UserId.ToString());
            await access.Save(ct);
        }
        return Ok(Mine(session, project, attempt));
    }

    private void RequireRunning(AssessmentSession session, OralAttempt attempt)
    {
        if (session.Status != "IN_PROGRESS") throw new ConflictException("Phiên không còn mở.");
        if (attempt.StartedAt == null) throw new ConflictException("Bạn chưa vào thi.");
        if (Finished(attempt, Now) != null) throw new ConflictException("Lượt thi đã kết thúc.");
    }

    [HttpPut("me/answers"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Answer(Guid id, SaveOralAnswerRequest request, CancellationToken ct)
    {
        var (session, _, attempt) = await LoadMine(id, ct);
        RequireRunning(session, attempt);
        if (Read<List<OralQuestionItem>>(attempt.QuestionsJson).All(q => q.Id != request.QuestionId))
            throw new BadRequestException("Câu hỏi không thuộc đề của bạn.");
        var answers = Read<List<OralAnswerItem>>(attempt.AnswersJson).Where(a => a.QuestionId != request.QuestionId).ToList();
        if (!string.IsNullOrWhiteSpace(request.Text)) answers.Add(new OralAnswerItem(request.QuestionId, request.Text.Trim(), Now));
        attempt.AnswersJson = Write(answers);
        attempt.Version = Guid.NewGuid();
        await access.Save(ct);
        return NoContent();
    }

    [HttpPost("me/submit"), Authorize(Roles = "Student")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken ct)
    {
        var (session, project, attempt) = await LoadMine(id, ct);
        if (attempt.SubmittedAt == null)
        {
            RequireRunning(session, attempt);
            attempt.SubmittedAt = Now;
            attempt.Version = Guid.NewGuid();
            access.Audit(id, "ORAL_ATTEMPT_SUBMITTED", current.UserId.ToString());
            await access.Save(ct);
        }
        return Ok(Mine(session, project, attempt));
    }

    // ----- Gợi ý rubric -----

    [HttpPost("~/api/assessments/rubric-suggestions"), Authorize(Roles = "Admin,Lecturer")]
    public async Task<IActionResult> SuggestRubric(RubricSuggestionRequest request, CancellationToken ct)
    {
        Class? courseClass;
        Course? course;
        if (request.ProjectId is Guid projectId)
        {
            var project = await access.Project(projectId, ct);
            await access.Staff(project, ct);
            courseClass = project.AnalysisRun.Group?.Class;
            course = courseClass?.Course ?? project.Course;
        }
        else
        {
            if (request.ClassId is not Guid classId) throw new BadRequestException("Cho biết lớp hoặc nhóm cần soạn rubric.");
            courseClass = await db.Classes.AsNoTracking().Include(c => c.Course).SingleOrDefaultAsync(c => c.Id == classId, ct)
                ?? throw new NotFoundException("Class unavailable.");
            var user = await access.User(ct);
            if (user.Role != UserRole.Admin && courseClass.LecturerId != user.Id) throw new ForbiddenException();
            course = courseClass.Course;
        }

        AssessmentRoundConfiguration? configuration = null;
        var roundTitle = request.RoundTitle;
        if (request.RoundRevisionId is Guid revisionId)
        {
            var revision = await db.AssessmentRoundRevisions.AsNoTracking()
                .SingleOrDefaultAsync(r => r.Id == revisionId && courseClass != null && r.Round.Milestone.ClassId == courseClass.Id, ct)
                ?? throw new BadRequestException("Đợt đánh giá không thuộc lớp này.");
            roundTitle ??= revision.Title;
            configuration = JsonSerializer.Deserialize<AssessmentRoundConfiguration>(revision.ConfigurationJson);
        }

        // Syllabus đang áp dụng cho kỳ của lớp: cột điểm, hướng dẫn chấm và chủ đề các tiết.
        var basedOn = new List<string>();
        var syllabus = new List<string>();
        if (courseClass != null)
        {
            var versions = await db.CourseSyllabi.AsNoTracking().Include(x => x.Semester).Where(x => x.CourseId == courseClass.CourseId).ToListAsync(ct);
            var start = await db.Semesters.Where(x => x.Id == courseClass.SemesterId).Select(x => (DateOnly?)x.StartDate).SingleOrDefaultAsync(ct);
            if (SyllabusRules.Effective(versions, start) is { } effective)
            {
                var view = SyllabusRules.View(effective);
                syllabus.AddRange(view.Assessments.Select(x => $"Cột điểm {x.Category} ({x.Weight}%)"
                    + (x.Clos is { Count: > 0 } ? $", CLO {string.Join(", ", x.Clos)}" : "")
                    + (string.IsNullOrWhiteSpace(x.GradingGuide) ? "" : $": {Shorten(x.GradingGuide)}")));
                syllabus.AddRange(view.Sessions.Select(x => $"Tiết {x.Number}: {Shorten(x.Topic)}").Take(40));
                basedOn.Add($"syllabus {view.Name}");
            }
        }

        var deliverables = request.Deliverables ?? configuration?.Deliverables ?? [];
        var outcomes = request.Outcomes ?? configuration?.Clos ?? [];
        var topics = request.Topics ?? configuration?.Topics ?? [];
        var guide = request.GradingGuide ?? configuration?.GradingGuide;
        if (deliverables.Count > 0) basedOn.Add("sản phẩm phải nộp");
        if (outcomes.Count > 0) basedOn.Add("chuẩn đầu ra của đợt");
        if (topics.Count > 0) basedOn.Add("chủ đề của đợt");
        if (!string.IsNullOrWhiteSpace(guide)) basedOn.Add("hướng dẫn chấm");

        var context = new RubricContext($"{course?.Code} {course?.Name}".Trim(), roundTitle, deliverables, outcomes, topics, guide, syllabus,
            Lines((request.Current ?? []).Where(c => !string.IsNullOrWhiteSpace(c.Title))), request.Notes);
        return Ok(new { criteria = await assistant.SuggestRubricAsync(context, ct), basedOn });
    }

    private static string Shorten(string text) => text.Length > 600 ? text[..600] + "…" : text;
}
