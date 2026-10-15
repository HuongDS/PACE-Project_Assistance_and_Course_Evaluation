using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos.StudentAnswer;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/student-answers")]
public class StudentAnswerController(IStudentAnswerService studentAnswerService) : ControllerBase
{
    [HttpPost("evaluate")]
    public async Task<IActionResult> EvaluateStudentAnswer([FromBody] StudentAnswerReq request)
    {
        var jsonResult = await studentAnswerService.EvaluateStudentAnswerAsync(request);
        return Content(jsonResult, "application/json");
    }
}
