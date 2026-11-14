using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Interfaces;
using PACE.Domain.Enums;
using PACE.Infrastructure.Persistence;
using PdfSharpCore;
using PdfSharpCore.Pdf;
using PdfSharpCore.Drawing;
using PdfSharpCore.Fonts;
using PdfSharpCore.Utils;

namespace PACE.API.Controllers;

public static class ReportPdf
{
    private static readonly object SyncLock = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (SyncLock)
        {
            if (_initialized) return;
            if (GlobalFontSettings.FontResolver == null)
            {
                GlobalFontSettings.FontResolver = new FontResolver();
            }
            _initialized = true;
        }
    }

    // Standard PDF document with embedded TrueType Unicode font (Arial) supporting full Vietnamese diacritics.
    public static byte[] Create(IEnumerable<string> lines)
    {
        EnsureInitialized();
        using var document = new PdfDocument();
        document.Info.Title = "PACE Assessment Report";
        document.Info.Author = "PACE System";

        var fontTitle = new XFont("Arial", 14, XFontStyle.Bold);
        var fontRegular = new XFont("Arial", 9.5, XFontStyle.Regular);
        var fontBold = new XFont("Arial", 9.5, XFontStyle.Bold);

        const double marginLeft = 40;
        const double marginTop = 45;
        const double marginBottom = 45;
        const double lineHeight = 15;
        const double printableWidth = 595 - (marginLeft * 2);

        PdfPage page = document.AddPage();
        page.Size = PageSize.A4;
        XGraphics gfx = XGraphics.FromPdfPage(page);
        double y = marginTop;

        void CheckNewPage(double needed = lineHeight)
        {
            if (y + needed > page.Height - marginBottom)
            {
                gfx.Dispose();
                page = document.AddPage();
                page.Size = PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = marginTop;
            }
        }

        bool isFirst = true;
        foreach (var rawLine in lines)
        {
            var text = rawLine ?? "";
            if (isFirst && (text.Contains("Report") || text.Contains("Báo cáo")))
            {
                CheckNewPage(28);
                gfx.DrawString(text, fontTitle, XBrushes.DarkSlateBlue, new XPoint(marginLeft, y));
                y += 24;
                isFirst = false;
                continue;
            }
            isFirst = false;

            XFont font = fontRegular;
            XBrush brush = XBrushes.Black;

            if (text.StartsWith("Student:") || text.StartsWith("Sinh viên:") ||
                text.StartsWith("Session:") || text.StartsWith("Course:") || text.StartsWith("Khóa học:") ||
                text.StartsWith("AI Conclusions:") || text.StartsWith("Kết luận AI:"))
            {
                font = fontBold;
                brush = XBrushes.Navy;
            }
            else if (text.StartsWith("  "))
            {
                font = fontRegular;
                brush = XBrushes.DarkSlateGray;
            }

            var words = text.Split(' ');
            var currentChunk = new StringBuilder();

            foreach (var word in words)
            {
                var candidate = currentChunk.Length == 0 ? word : currentChunk + " " + word;
                var measurement = gfx.MeasureString(candidate, font);
                if (measurement.Width > printableWidth && currentChunk.Length > 0)
                {
                    CheckNewPage(lineHeight);
                    gfx.DrawString(currentChunk.ToString(), font, brush, new XPoint(marginLeft, y));
                    y += lineHeight;
                    currentChunk.Clear();
                    currentChunk.Append(word);
                }
                else
                {
                    if (currentChunk.Length > 0) currentChunk.Append(' ');
                    currentChunk.Append(word);
                }
            }

            if (currentChunk.Length > 0)
            {
                CheckNewPage(lineHeight);
                gfx.DrawString(currentChunk.ToString(), font, brush, new XPoint(marginLeft, y));
                y += lineHeight;
            }
        }

        gfx.Dispose();

        using var ms = new MemoryStream();
        document.Save(ms);
        return ms.ToArray();
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
