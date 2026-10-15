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
    [Authorize(Roles = Roles.Student)]
    [HttpPost("lookup")]
    public async Task<ActionResult<StudentSubmissionLookupDto>> Lookup(CancellationToken ct)
        => Ok(await service.LookupAsync(ct));

    [Authorize(Roles = Roles.Student)]
    [HttpPost]
    public async Task<ActionResult<StudentSubmissionCreatedDto>> Submit(SubmitStudentSubmissionRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await service.SubmitAsync(request, ct));

    [Authorize(Roles = Roles.Student)]
    [HttpPost("retrieve")]
    public async Task<ActionResult<StudentSubmissionDetailDto>> Retrieve(RetrieveStudentSubmissionRequest request, CancellationToken ct)
        => Ok(await service.RetrieveAsync(request, ct));

    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PagedResult<AdminStudentSubmissionDto>>> GetAll([FromQuery] StudentSubmissionQuery query, CancellationToken ct)
        => Ok(await service.GetAllAsync(query, ct));
}
