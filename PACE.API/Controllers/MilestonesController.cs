using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Authorize]
public class MilestonesController(IMilestoneService milestoneService) : ControllerBase
{
    [HttpGet("api/classes/{classId:guid}/project-milestones")]
    public async Task<ActionResult<List<MilestoneDto>>> GetByClass(Guid classId, CancellationToken ct)
    {
        var result = await milestoneService.GetByClassIdAsync(classId, ct);
        return Ok(result);
    }

    [HttpGet("api/project-milestones/{id:guid}")]
    public async Task<ActionResult<MilestoneDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await milestoneService.GetByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost("api/classes/{classId:guid}/project-milestones")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Lecturer}")]
    public async Task<ActionResult<MilestoneDto>> Create(Guid classId, [FromBody] CreateMilestoneRequest request, CancellationToken ct)
    {
        var result = await milestoneService.CreateAsync(classId, request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("api/project-milestones/{id:guid}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Lecturer}")]
    public async Task<ActionResult<MilestoneDto>> Update(Guid id, [FromBody] UpdateMilestoneRequest request, CancellationToken ct)
    {
        var result = await milestoneService.UpdateAsync(id, request, ct);
        return Ok(result);
    }

    [HttpDelete("api/project-milestones/{id:guid}")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Lecturer}")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await milestoneService.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPatch("api/project-milestones/{id:guid}/lock")]
    [Authorize(Roles = $"{Roles.Admin},{Roles.Lecturer}")]
    public async Task<ActionResult<MilestoneDto>> ToggleLock(Guid id, [FromBody] ToggleMilestoneLockRequest request, CancellationToken ct)
    {
        var result = await milestoneService.ToggleLockAsync(id, request.IsLocked, ct);
        return Ok(result);
    }
}
