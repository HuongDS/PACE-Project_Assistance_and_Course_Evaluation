using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/business-flows")]
[Authorize(Roles = Roles.Admin)]
public class BusinessFlowsController(IBusinessFlowService flowService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessFlowDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await flowService.GetDetailAsync(id, ct));
}
