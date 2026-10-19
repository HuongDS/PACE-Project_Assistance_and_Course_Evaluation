using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Common.Models;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/student-submissions")]
[Authorize]
public class StudentSubmissionsController(IStudentSubmissionService service) : ControllerBase
{
    [HttpPost("join")]
    [AllowAnonymous]
    public async Task<ActionResult<StudentSubmissionJoinedDto>> Join([FromBody] JoinStudentSubmissionRequest request, CancellationToken ct)
        => Ok(await service.JoinAsync(request, ct));

    [Authorize(Roles = Roles.Student)]
    [HttpPost("lookup")]
    public async Task<ActionResult<StudentSubmissionLookupDto>> Lookup(CancellationToken ct)
        => Ok(await service.LookupAsync(ct));

    [Authorize(Roles = Roles.Student)]
    [HttpPost]
    public async Task<ActionResult<StudentSubmissionResultDto>> Submit([FromBody] SubmitStudentSubmissionRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await service.SubmitAsync(request, ct));

    [Authorize(Roles = Roles.Student)]
    [HttpPost("retrieve")]
    public async Task<ActionResult<StudentSubmissionDetailDto>> Retrieve([FromBody] RetrieveStudentSubmissionRequest request, CancellationToken ct)
        => Ok(await service.RetrieveAsync(request, ct));

    [HttpGet("me")]
    [Authorize(Roles = Roles.Student)]
    public async Task<ActionResult<PagedResult<StudentSubmissionDetailDto>>> GetMine([FromQuery] StudentSubmissionQuery query, CancellationToken ct)
        => Ok(await service.GetMineAsync(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Admin + "," + Roles.Student)]
    public async Task<ActionResult<StudentSubmissionDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await service.GetByIdAsync(id, ct));

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PagedResult<AdminStudentSubmissionDto>>> GetAll([FromQuery] StudentSubmissionQuery query, CancellationToken ct)
        => Ok(await service.GetAllAsync(query, ct));

    [HttpPost("admin/quick-create")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AdminStudentSubmissionDto>> QuickCreate([FromBody] QuickCreateStudentSubmissionRequest request, CancellationToken ct)
        => Ok(await service.QuickCreateAsync(request, ct));
}
