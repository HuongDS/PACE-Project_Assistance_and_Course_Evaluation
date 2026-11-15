using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/lecturer-profiles")]
[Authorize]
public class LecturerProfilesController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<LecturerProfileDto>>> GetAll([FromQuery] LecturerProfileQuery query, CancellationToken ct)
        => Ok(await profileService.GetLecturerProfilesPagedAsync(query, ct));

    [HttpGet("{lecturerCode}")]
    public async Task<ActionResult<LecturerProfileDto>> GetByCode(string lecturerCode, CancellationToken ct)
        => Ok(await profileService.GetLecturerProfileByCodeAsync(lecturerCode, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<LecturerProfileDto>> Create([FromBody] CreateLecturerProfileRequest request, CancellationToken ct)
    {
        var result = await profileService.CreateLecturerProfileAsync(request, ct);
        return CreatedAtAction(nameof(GetByCode), new { lecturerCode = result.LecturerCode }, result);
    }

    [HttpPut("{lecturerCode}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<LecturerProfileDto>> Update(string lecturerCode, [FromBody] UpdateLecturerProfileRequest request, CancellationToken ct)
        => Ok(await profileService.UpdateLecturerProfileAsync(lecturerCode, request, ct));

    [HttpDelete("{lecturerCode}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(string lecturerCode, CancellationToken ct)
    {
        await profileService.DeleteLecturerProfileAsync(lecturerCode, ct);
        return NoContent();
    }
}
