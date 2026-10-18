using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.Application.Common.Interfaces;
using PACE.Application.Dtos.StudentAnswer;
using PACE.Application.Services.Interfaces;
using PACE.Infrastructure.Persistence;
namespace PACE.API.Controllers;
[ApiController, Route("api/student-answers"), Authorize(Roles = "Student")]
public class StudentAnswerController(ICurrentUserService current, AppDbContext db) : ControllerBase
{
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateStudentAnswer(StudentAnswerReq request, CancellationToken ct)
    {
        // Evaluation operates only on a persisted answer belonging to the authenticated student.
        var answer = await db.StudentAnswers.AsNoTracking().FirstOrDefaultAsync(a => a.UserId == current.UserId && a.SubmissionId == request.SubmissionId && a.QuestionId == request.QuestionId, ct);
        if (answer is null) return NotFound();
        if (await db.GeneratedQuestions.AnyAsync(q => q.Id == answer.QuestionId && q.BusinessId != request.BusinessId, ct)) return BadRequest();
        if (!string.IsNullOrWhiteSpace(answer.AiFeedback)) return Content(answer.AiFeedback, "application/json");
        // Normal submission already invokes the provider. Retrying through a second endpoint can bill twice.
        return Conflict(new { error = "Evaluation is unavailable or still processing. Retrieve the submission; automatic duplicate evaluation is disabled." });
    }
}
