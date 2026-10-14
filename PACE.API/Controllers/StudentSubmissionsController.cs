using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/student-submissions")]
[AllowAnonymous]
public class StudentSubmissionsController(IStudentSubmissionService service) : ControllerBase
{
    [HttpPost("lookup")]
    public async Task<ActionResult<StudentSubmissionLookupDto>> Lookup(StudentIdentityRequest request, CancellationToken ct)
        => Ok(await service.LookupAsync(request, ct));

    [HttpPost]
    public async Task<ActionResult<StudentSubmissionCreatedDto>> Submit(SubmitStudentSubmissionRequest request, CancellationToken ct)
        => StatusCode(StatusCodes.Status201Created, await service.SubmitAsync(request, ct));

    [HttpPost("retrieve")]
    public async Task<ActionResult<StudentSubmissionDetailDto>> Retrieve(RetrieveStudentSubmissionRequest request, CancellationToken ct)
        => Ok(await service.RetrieveAsync(request, ct));
}
