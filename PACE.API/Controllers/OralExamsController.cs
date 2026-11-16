using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/oral-exams")]
[Authorize]
public class OralExamsController(IOralPracticeService oralPracticeService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<OralExamDto>>> GetAll([FromQuery] OralExamQuery query, CancellationToken ct)
        => Ok(await oralPracticeService.GetOralExamsPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OralExamDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await oralPracticeService.GetOralExamDetailAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<OralExamDto>> Create([FromBody] CreateOralExamRequest request, CancellationToken ct)
    {
        var result = await oralPracticeService.CreateOralExamAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<OralExamDto>> Update(Guid id, [FromBody] UpdateOralExamRequest request, CancellationToken ct)
        => Ok(await oralPracticeService.UpdateOralExamAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await oralPracticeService.DeleteOralExamAsync(id, ct);
        return NoContent();
    }
}
