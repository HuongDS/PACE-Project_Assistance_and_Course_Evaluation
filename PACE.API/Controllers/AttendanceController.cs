using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/attendance")]
[Authorize]
public class AttendanceController(IAttendanceService attendanceService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<AttendanceDto>>> GetAll([FromQuery] AttendanceQuery query, CancellationToken ct)
        => Ok(await attendanceService.GetPagedAsync(query, ct));

    [HttpGet("sessions/{sessionId:guid}")]
    public async Task<ActionResult<List<AttendanceDto>>> GetBySession(Guid sessionId, CancellationToken ct)
        => Ok(await attendanceService.GetBySessionIdAsync(sessionId, ct));

    [HttpPost("sessions/{sessionId:guid}/batch")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<List<AttendanceDto>>> BatchRecord(Guid sessionId, [FromBody] BatchRecordAttendanceRequest request, CancellationToken ct)
        => Ok(await attendanceService.BatchRecordAsync(sessionId, request, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<AttendanceDto>> Update(Guid id, [FromBody] UpdateAttendanceRequest request, CancellationToken ct)
        => Ok(await attendanceService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await attendanceService.DeleteAsync(id, ct);
        return NoContent();
    }
}
