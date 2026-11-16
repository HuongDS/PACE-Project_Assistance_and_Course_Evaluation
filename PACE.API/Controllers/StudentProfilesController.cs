using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/student-profiles")]
[Authorize]
public class StudentProfilesController(IProfileService profileService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<StudentProfileDto>>> GetAll([FromQuery] StudentProfileQuery query, CancellationToken ct)
        => Ok(await profileService.GetStudentProfilesPagedAsync(query, ct));

    [HttpGet("{studentCode}")]
    public async Task<ActionResult<StudentProfileDto>> GetByCode(string studentCode, CancellationToken ct)
        => Ok(await profileService.GetStudentProfileByCodeAsync(studentCode, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<StudentProfileDto>> Create([FromBody] CreateStudentProfileRequest request, CancellationToken ct)
    {
        var result = await profileService.CreateStudentProfileAsync(request, ct);
        return CreatedAtAction(nameof(GetByCode), new { studentCode = result.StudentCode }, result);
    }

    [HttpPut("{studentCode}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<StudentProfileDto>> Update(string studentCode, [FromBody] UpdateStudentProfileRequest request, CancellationToken ct)
        => Ok(await profileService.UpdateStudentProfileAsync(studentCode, request, ct));

    [HttpDelete("{studentCode}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(string studentCode, CancellationToken ct)
    {
        await profileService.DeleteStudentProfileAsync(studentCode, ct);
        return NoContent();
    }
}
