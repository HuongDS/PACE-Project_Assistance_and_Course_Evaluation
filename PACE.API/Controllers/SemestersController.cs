using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PACE.API.Common;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;
using PACE.Infrastructure.Persistence;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/semesters")]
[Authorize]
[AllowTrainingOffice]
public class SemestersController(ISemesterService semesterService, AppDbContext db) : ControllerBase
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
    [Authorize(Roles = Roles.Registrar)]
    public async Task<ActionResult<SemesterDto>> Create([FromBody] CreateSemesterRequest request, CancellationToken ct)
    {
        var result = await semesterService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Registrar)]
    public async Task<ActionResult<SemesterDto>> Update(Guid id, [FromBody] UpdateSemesterRequest request, CancellationToken ct)
        => Ok(await semesterService.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Registrar)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (await db.CourseSyllabi.AnyAsync(x => x.SemesterId == id, ct))
            throw new ConflictException("Có syllabus áp dụng từ học kỳ này. Gỡ hoặc nhập lại syllabus đó trước khi xóa.");
        await semesterService.DeleteAsync(id, ct);
        return NoContent();
    }
}

