using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.BLL.Dtos;
using PACE.BLL.Services.Interfaces;

namespace PACE.Api.Controllers;

[ApiController]
[Route("api/rubric-criteria")]
[Authorize]
public class RubricCriteriaController(IRubricCriterionService criterionService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RubricCriterionDto>>> GetAll(CancellationToken ct)
        => Ok(await criterionService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RubricCriterionDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await criterionService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<RubricCriterionDto>> Create(UpsertRubricCriterionRequest request, CancellationToken ct)
    {
        var criterion = await criterionService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = criterion.Id }, criterion);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<RubricCriterionDto>> Update(Guid id, UpsertRubricCriterionRequest request, CancellationToken ct)
        => Ok(await criterionService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await criterionService.DeleteAsync(id, ct);
        return NoContent();
    }
}
