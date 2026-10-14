using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/businesses")]
[Authorize(Roles = Roles.Admin)]
public class BusinessesController(IBusinessService businessService, IBusinessFlowService flowService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BusinessDto>>> GetByRun([FromQuery] Guid analysisRunId, CancellationToken ct)
        => Ok(await businessService.GetByRunAsync(analysisRunId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await businessService.GetDetailAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BusinessDto>> Update(Guid id, UpdateBusinessRequest request, CancellationToken ct)
        => Ok(await businessService.UpdateAsync(id, request, ct));

    [HttpGet("{id:guid}/flows")]
    public async Task<ActionResult<IReadOnlyList<BusinessFlowSummaryDto>>> GetFlows(Guid id, CancellationToken ct)
        => Ok(await flowService.GetByBusinessAsync(id, ct));
}
