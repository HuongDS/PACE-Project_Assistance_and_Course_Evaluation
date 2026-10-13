using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.BLL.Dtos;
using PACE.BLL.Services.Interfaces;

namespace PACE.Api.Controllers;

[ApiController]
[Route("api/business-flows")]
[Authorize(Roles = Roles.Admin)]
public class BusinessFlowsController(IBusinessFlowService flowService) : ControllerBase
{
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessFlowDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await flowService.GetDetailAsync(id, ct));
}
