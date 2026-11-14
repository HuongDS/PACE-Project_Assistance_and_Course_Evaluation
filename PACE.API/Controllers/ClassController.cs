using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Exceptions;
using PACE.Application.Common.Models;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/classes")]
[Authorize]
public class ClassController(IClassService classService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClassDto>>> GetAll([FromQuery] ClassQuery query, CancellationToken ct)
        => Ok(await classService.GetPagedAsync(query, ct));

    [HttpGet("template")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
    {
        var (fileBytes, fileName) = await classService.ExportClassTemplateAsync(ct);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<ClassDto>> Create(
        [FromBody] CreateClassRequest request,
        CancellationToken ct)
    {
        var result = await classService.CreateClassAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("import")]
    [Authorize(Roles = Roles.Staff)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportClassesResult>> ImportClasses(
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("Chưa chọn file Excel.");

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ hỗ trợ file Excel định dạng .xlsx.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;

        var result = await classService.ImportClassesAsync(stream, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClassDetailDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await classService.GetDetailAsync(id, ct));

    [HttpGet("{id:guid}/students")]
    public async Task<ActionResult<PagedResult<ClassStudentDto>>> GetStudents(
        Guid id,
        [FromQuery] ClassStudentQuery query,
        CancellationToken ct)
        => Ok(await classService.GetStudentsAsync(id, query, ct));

    [HttpPost("{id:guid}/students")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<ActionResult<object>> AddStudents(
        Guid id,
        AddStudentsToClassRequest request,
        CancellationToken ct)
    {
        var count = await classService.AddStudentsAsync(id, request, ct);
        return Ok(new { addedCount = count });
    }

    [HttpDelete("{id:guid}/students/{studentCode}")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> RemoveStudent(Guid id, string studentCode, CancellationToken ct)
    {
        await classService.RemoveStudentAsync(id, studentCode, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/students/import")]
    [Authorize(Roles = Roles.Staff)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ImportClassStudentsResult>> ImportStudents(
        Guid id,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("Chưa chọn file Excel.");

        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ hỗ trợ file Excel định dạng .xlsx.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;

        var result = await classService.ImportStudentsAsync(id, stream, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/students/export")]
    [Authorize(Roles = Roles.Staff)]
    public async Task<IActionResult> ExportStudents(Guid id, CancellationToken ct)
    {
        var (fileBytes, fileName) = await classService.ExportStudentsAsync(id, ct);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}

