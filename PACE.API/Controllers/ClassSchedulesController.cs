using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/class-schedules")]
[Authorize]
public class ClassSchedulesController(ITimetableService timetableService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClassScheduleDto>>> GetAll([FromQuery] ClassScheduleQuery query, CancellationToken ct)
        => Ok(await timetableService.GetSchedulesPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClassScheduleDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await timetableService.GetScheduleByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassScheduleDto>> Create([FromBody] CreateClassScheduleRequest request, CancellationToken ct)
    {
        var result = await timetableService.CreateScheduleAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassScheduleDto>> Update(Guid id, [FromBody] UpdateClassScheduleRequest request, CancellationToken ct)
        => Ok(await timetableService.UpdateScheduleAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await timetableService.DeleteScheduleAsync(id, ct);
        return NoContent();
    }
}
