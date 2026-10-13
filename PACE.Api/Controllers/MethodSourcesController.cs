using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.BLL.Dtos;
using PACE.BLL.Services.Interfaces;
using PACE.DAL.Repositories;

namespace PACE.Api.Controllers;

[ApiController]
[Route("api/method-sources")]
[Authorize(Roles = Roles.Admin)]
public class MethodSourcesController(IMethodSourceService methodService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<MethodSourceSummaryDto>>> GetAll([FromQuery] MethodSourceQuery query, CancellationToken ct)
        => Ok(await methodService.GetPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MethodSourceDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await methodService.GetByIdAsync(id, ct));
}
