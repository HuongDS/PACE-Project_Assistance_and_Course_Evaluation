using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/class-sessions")]
[Authorize]
public class ClassSessionsController(ITimetableService timetableService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClassSessionDto>>> GetAll([FromQuery] ClassSessionQuery query, CancellationToken ct)
        => Ok(await timetableService.GetSessionsPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClassSessionDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await timetableService.GetSessionByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassSessionDto>> Create([FromBody] CreateClassSessionRequest request, CancellationToken ct)
    {
        var result = await timetableService.CreateSessionAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassSessionDto>> Update(Guid id, [FromBody] UpdateClassSessionRequest request, CancellationToken ct)
        => Ok(await timetableService.UpdateSessionAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await timetableService.DeleteSessionAsync(id, ct);
        return NoContent();
    }
}
