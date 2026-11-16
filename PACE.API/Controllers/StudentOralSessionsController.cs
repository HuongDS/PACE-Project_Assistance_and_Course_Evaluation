using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/student-oral-sessions")]
[Authorize]
public class StudentOralSessionsController(IOralPracticeService oralPracticeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<StudentOralSessionDto>>> GetAll([FromQuery] StudentOralSessionQuery query, CancellationToken ct)
        => Ok(await oralPracticeService.GetSessionsPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StudentOralSessionDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await oralPracticeService.GetSessionDetailAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<StudentOralSessionDto>> Start([FromBody] StartStudentOralSessionRequest request, CancellationToken ct)
    {
        var result = await oralPracticeService.StartSessionAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<StudentOralSessionDto>> Update(Guid id, [FromBody] UpdateStudentOralSessionRequest request, CancellationToken ct)
        => Ok(await oralPracticeService.UpdateSessionAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await oralPracticeService.DeleteSessionAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/answers")]
    public async Task<ActionResult<StudentOralAnswerDto>> SubmitAnswer(Guid id, [FromBody] SubmitOralAnswerRequest request, CancellationToken ct)
        => Ok(await oralPracticeService.SubmitAnswerAsync(id, request, ct));

    [HttpPut("{id:guid}/answers/{answerId:guid}")]
    public async Task<ActionResult<StudentOralAnswerDto>> UpdateAnswer(Guid id, Guid answerId, [FromBody] UpdateOralAnswerRequest request, CancellationToken ct)
        => Ok(await oralPracticeService.UpdateAnswerAsync(id, answerId, request, ct));
}
