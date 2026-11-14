using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/semesters")]
[Authorize]
public class SemestersController(ISemesterService semesterService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SemesterDto>>> GetAll(CancellationToken ct)
        => Ok(await semesterService.GetAllAsync(ct));

    [HttpGet("current")]
    public async Task<ActionResult<SemesterDto>> GetCurrent(CancellationToken ct)
    {
        var current = await semesterService.GetCurrentSemesterAsync(ct);
        if (current == null) return NoContent();
        return Ok(current);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SemesterDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await semesterService.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<SemesterDto>> Create([FromBody] CreateSemesterRequest request, CancellationToken ct)
    {
        var result = await semesterService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<SemesterDto>> Update(Guid id, [FromBody] UpdateSemesterRequest request, CancellationToken ct)
        => Ok(await semesterService.UpdateAsync(id, request, ct));
}

