using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;
namespace PACE.API.Controllers;

public static class ReportPdf
{
    // Text PDF with explicit object byte offsets, page tree, cross-reference table and full UTF-8 text support.
    public static byte[] Create(IEnumerable<string> lines)
    {
        var rawLines = lines.ToList();
        var pages = rawLines.SelectMany(x => Enumerable.Range(0, Math.Max(1, (x.Length + 87) / 88)).Select(i => x.Substring(i * 88, Math.Min(88, x.Length - i * 88)))).Chunk(48).ToArray();
        if (pages.Length == 0) pages = [[]];
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "", "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>" };
        var pageIds = new List<int>();
        foreach (var page in pages)
        {
            int id = objects.Count + 1;
            pageIds.Add(id);
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {id + 1} 0 R >>");
            var stream = "BT /F1 10 Tf 40 752 Td 14 TL\n" + string.Join("\n", page.Select(l => "(" + EscapePdfText(l) + ") Tj T*")) + "\nET";
            objects.Add($"<< /Length {Encoding.UTF8.GetByteCount(stream)} >>\nstream\n{stream}\nendstream");
        }
        objects[1] = $"<< /Type /Pages /Count {pages.Length} /Kids [{string.Join(" ", pageIds.Select(x => $"{x} 0 R"))}] >>";

        var offsets = new List<int> { 0 };
        var header = "%PDF-1.4\n";
        var bodyBuilder = new StringBuilder(header);
        int currentOffset = Encoding.UTF8.GetByteCount(header);

        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(currentOffset);
            var objStr = $"{i + 1} 0 obj\n{objects[i]}\nendobj\n";
            bodyBuilder.Append(objStr);
            currentOffset += Encoding.UTF8.GetByteCount(objStr);
        }

        var xrefOffset = currentOffset;
        var xrefBuilder = new StringBuilder();
        xrefBuilder.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var n in offsets.Skip(1))
        {
            xrefBuilder.Append($"{n:D10} 00000 n \n");
        }
        xrefBuilder.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF\n");

        bodyBuilder.Append(xrefBuilder.ToString());
        return Encoding.UTF8.GetBytes(bodyBuilder.ToString());
    }

    private static string EscapePdfText(string text)
    {
        return text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    }
}

[ApiController, Route("api/reports"), Authorize]
public class ReportsController(AppDbContext db, TeachingAccess access) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string? ComputeGradeLetter(decimal? score) => score switch
    {
        null => null,
        >= 85m => "A",
        >= 70m => "B",
        >= 55m => "C",
        >= 40m => "D",
        _ => "F"
    };

    private async Task<(object Report, List<string> Lines)> Build(Guid id, CancellationToken ct)
    {
        var s = await db.AssessmentSessions.Include(x => x.Students).ThenInclude(x => x.Student).AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Report unavailable.");
        var p = await access.Project(s.ProjectId, ct); var u = await access.User(ct); if (u.Role == UserRole.Student && !s.Students.Any(x => x.StudentId == u.Id)) throw new ForbiddenException();
        SessionRules.Require(s.Status, "COMPLETED"); var rubric = JsonSerializer.Deserialize<List<SessionCriterion>>(s.RubricJson, Json)!;
        var visible = s.Students.Where(x => u.Role != UserRole.Student || x.StudentId == u.Id);
        var scores = visible.Select(x =>
        {
            var evaluations = x.EvaluationJson == null ? null : JsonSerializer.Deserialize<List<ScoreInput>>(x.EvaluationJson, Json);
            decimal? score = evaluations == null ? null : evaluations.Sum(v =>
            {
                var c = rubric.FirstOrDefault(c => c.Id == v.CriterionId);
                return c == null || c.MaxScore <= 0 ? 0 : v.Score / c.MaxScore * c.Weight;
            });
            string? gradeLetter = ComputeGradeLetter(score);
            return new { studentId = x.StudentId, studentName = x.Student.Email, score, gradeLetter, evaluations, x.EvaluatedAt, x.EvaluatorId };
        }).ToArray();
        var timeline = await db.AuditEvents.AsNoTracking().Where(x => x.ResourceId == id).OrderBy(x => x.Timestamp).Take(1000).Select(x => new { x.Id, x.Timestamp, x.EventType, x.ActorName, x.Details }).ToListAsync(ct);
        var duration = (int?)((s.ActualEndTime - s.ActualStartTime)?.TotalSeconds);

        var conclusions = scores
            .Where(sc => sc.evaluations != null && sc.evaluations.Count > 0)
            .Select(sc =>
            {
                var feedbackItems = sc.evaluations!
                    .Where(e => !string.IsNullOrWhiteSpace(e.Feedback))
                    .Select(e => $"{rubric.FirstOrDefault(c => c.Id == e.CriterionId)?.Title ?? "Criterion"}: {e.Feedback}")
                    .ToList();
                return feedbackItems.Count > 0
                    ? $"{sc.studentName} ({sc.gradeLetter ?? "N/A"}): {string.Join("; ", feedbackItems)}"
                    : null;
            })
            .Where(c => c != null)
            .ToList();

        var aiConclusionText = conclusions.Count > 0
            ? string.Join(" | ", conclusions)
            : "No student evaluation feedback recorded.";

        var report = new
        {
            id = s.Id,
            sessionId = s.Id,
            courseCode = p.Course.Code,
            groupName = p.AnalysisRun.RepoName,
            totalDurationSeconds = duration,
            finalScores = scores,
            timeline,
            generatedAt = DateTime.UtcNow,
            rubric,
            repositoryUrl = p.AnalysisRun.RepoUrl,
            aiConclusions = aiConclusionText
        };
        var lines = new List<string>
        {
            "PACE Assessment Report",
            $"Session: {s.Id}",
            $"Title: {s.Title}",
            $"Course: {p.Course.Code} / {p.Course.Semester}",
            $"Project: {p.AnalysisRun.RepoName}",
            $"Started (UTC): {s.ActualStartTime:O}",
            $"Completed (UTC): {s.ActualEndTime:O}",
            "Scores are normalized to 100 using the session rubric.",
            $"AI Conclusions: {aiConclusionText}"
        };
        foreach (var score in scores)
        {
            lines.Add($"Student: {score.studentName}; Score: {score.score?.ToString("0.##") ?? "Unavailable"}; Grade: {score.gradeLetter ?? "N/A"}");
            foreach (var e in score.evaluations ?? [])
            {
                lines.Add($"  {rubric.FirstOrDefault(c => c.Id == e.CriterionId)?.Title ?? "Criterion"}: {e.Score}; {e.Feedback}");
            }
        }
        foreach (var e in timeline)
        {
            lines.Add($"{e.Timestamp:O} {e.EventType} {e.ActorName} {e.Details}");
        }
        return (report, lines);
    }
    [ProducesResponseType(typeof(AssessmentReportView), 200)]
    [HttpGet("{id:guid}")] public async Task<IActionResult> Get(Guid id, CancellationToken ct) => Ok((await Build(id, ct)).Report);
    [Produces("application/pdf")]
    [HttpGet("{id:guid}/export/pdf")] public async Task<IActionResult> Pdf(Guid id, CancellationToken ct) => File(ReportPdf.Create((await Build(id, ct)).Lines), "application/pdf", $"assessment-{id}.pdf");
}
[ApiController, Route("api/audit-events"), Authorize(Roles = "Admin")]
public class AuditEventsController(AppDbContext db) : ControllerBase
{
    [HttpGet] public async Task<IActionResult> List([FromQuery, System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)] int page = 1, [FromQuery, System.ComponentModel.DataAnnotations.Range(1, 100)] int pageSize = 20, CancellationToken ct = default) => Ok(new { items = await db.AuditEvents.AsNoTracking().OrderByDescending(x => x.Timestamp).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct), page, pageSize, totalCount = await db.AuditEvents.CountAsync(ct) });
}
