using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/oral-questions")]
[Authorize]
public class OralQuestionsController(IOralPracticeService oralPracticeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<OralQuestionDto>>> GetAll([FromQuery] OralQuestionQuery query, CancellationToken ct)
        => Ok(await oralPracticeService.GetQuestionsPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OralQuestionDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await oralPracticeService.GetQuestionByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<OralQuestionDto>> Create([FromBody] CreateOralQuestionRequest request, CancellationToken ct)
    {
        var result = await oralPracticeService.CreateQuestionAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<OralQuestionDto>> Update(Guid id, [FromBody] UpdateOralQuestionRequest request, CancellationToken ct)
        => Ok(await oralPracticeService.UpdateQuestionAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await oralPracticeService.DeleteQuestionAsync(id, ct);
        return NoContent();
    }
}
