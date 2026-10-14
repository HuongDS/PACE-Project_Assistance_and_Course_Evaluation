using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

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
