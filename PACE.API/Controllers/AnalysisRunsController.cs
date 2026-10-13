using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/analysis-runs")]
[Authorize]
public class AnalysisRunsController(IAnalysisRunService runService, IRepoAnalysisService analysisService) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<PagedResult<AnalysisRunDto>>> GetAll([FromQuery] AnalysisRunQuery query, CancellationToken ct)
        => Ok(await runService.GetPagedAsync(query, ct));

    [HttpGet("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AnalysisRunDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await runService.GetDetailAsync(id, ct));

    [HttpGet("me")]
    public async Task<ActionResult<AnalysisRunDetailDto>> GetMine(CancellationToken ct)
        => Ok(await runService.GetMineAsync(ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AnalyzeResultDto>> CreateAndAnalyze(CreateAnalysisRunRequest request)
        => Ok(await analysisService.CreateAndAnalyzeAsync(request, CancellationToken.None));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<AnalysisRunDto>> Update(Guid id, UpdateAnalysisRunRequest request, CancellationToken ct)
        => Ok(await runService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await runService.DeleteAsync(id, ct);
        return NoContent();
    }
}
