using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Exceptions;
using PACE.Domain.Entities;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

public record OralQuestionInput([Required, MaxLength(4000)] string Content, Guid? GeneratedQuestionId = null);
public record CreateAssessmentOralExamRequest(Guid ProjectId, [Required, MaxLength(255)] string Title,
    [Required, MinLength(1), MaxLength(100)] List<OralQuestionInput> Questions,
    [MaxLength(4000)] string? Description = null);
public record OralQuestionView(Guid Id, string Content);

[ApiController, Route("api/assessments/oral-exams"), Authorize(Roles = "Admin,Lecturer")]
public class AssessmentOralExamsController(AppDbContext db, TeachingAccess access) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] Guid projectId, CancellationToken ct)
    {
        var project = await access.Project(projectId, ct);
        await access.Staff(project, ct);
        var classId = project.AnalysisRun.Group?.ClassId;
        if (classId is null) return Ok(Array.Empty<object>());

        return Ok(await db.OralExams.AsNoTracking().Where(exam => exam.ClassId == classId)
            .OrderByDescending(exam => exam.CreatedAt)
            .Select(exam => new { exam.Id, exam.Title, exam.Description, QuestionCount = exam.Questions.Count })
            .ToListAsync(ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, [FromQuery] Guid projectId, CancellationToken ct)
    {
        var project = await access.Project(projectId, ct);
        await access.Staff(project, ct);
        var classId = project.AnalysisRun.Group?.ClassId;
        var exam = await db.OralExams.AsNoTracking().Where(exam => exam.Id == id && exam.ClassId == classId)
            .Select(exam => new { exam.Id, exam.Title, exam.Description,
                Questions = exam.Questions.OrderBy(question => question.Id)
                    .Select(question => new OralQuestionView(question.Id, question.Content)).ToList() })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("Oral exam unavailable.");
        return Ok(exam);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateAssessmentOralExamRequest request, CancellationToken ct)
    {
        var project = await access.Project(request.ProjectId, ct);
        await access.Staff(project, ct);
        var classId = project.AnalysisRun.Group?.ClassId
            ?? throw new BadRequestException("Assign the project to a class before preparing an oral exam.");
        if (request.Questions.Any(question => question is null))
            throw new BadRequestException("Provide valid oral questions.");

        var generatedIds = request.Questions.Where(question => question.GeneratedQuestionId.HasValue)
            .Select(question => question.GeneratedQuestionId!.Value).Distinct().ToArray();
        var availableCount = await db.GeneratedQuestions.CountAsync(question => generatedIds.Contains(question.Id)
            && question.Business.AnalysisRunId == request.ProjectId, ct);
        if (availableCount != generatedIds.Length)
            throw new BadRequestException("Generated questions must belong to the project.");

        var exam = new OralExam
        {
            ClassId = classId,
            Title = request.Title.Trim(),
            Description = request.Description?.Trim(),
            Questions = request.Questions.Select(question => new OralQuestion
            {
                Content = question.Content.Trim(),
                GeneratedQuestionId = question.GeneratedQuestionId,
                ReferenceContext = question.GeneratedQuestionId.HasValue ? project.AnalysisRun.AnalyzedCommitSha : null
            }).ToList()
        };

        db.OralExams.Add(exam);
        access.Audit(exam.Id, "ORAL_EXAM_CREATED", exam.Title);
        await access.Save(ct);
        return CreatedAtAction(nameof(Get), new { id = exam.Id, projectId = request.ProjectId }, new { exam.Id });
    }
}
