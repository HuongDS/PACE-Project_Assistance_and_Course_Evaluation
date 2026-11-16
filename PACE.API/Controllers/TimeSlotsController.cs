using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/time-slots")]
[Authorize]
public class TimeSlotsController(ITimetableService timetableService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<TimeSlotDto>>> GetAll(CancellationToken ct)
        => Ok(await timetableService.GetAllTimeSlotsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TimeSlotDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await timetableService.GetTimeSlotByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TimeSlotDto>> Create([FromBody] CreateTimeSlotRequest request, CancellationToken ct)
    {
        var result = await timetableService.CreateTimeSlotAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<TimeSlotDto>> Update(Guid id, [FromBody] UpdateTimeSlotRequest request, CancellationToken ct)
        => Ok(await timetableService.UpdateTimeSlotAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await timetableService.DeleteTimeSlotAsync(id, ct);
        return NoContent();
    }
}
