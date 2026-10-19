using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;
using PACE.Application.Common.Exceptions;
using System.Security.Claims;

namespace PACE.API.Controllers;

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

    [HttpPost("bulk")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<BulkImportRubricCriteriaResult>> BulkImport([FromBody] List<UpsertRubricCriterionRequest> items, CancellationToken ct)
        => Ok(await criterionService.BulkImportAsync(items, ct));

    [HttpPost("import-file")]
    [Authorize(Roles = Roles.Admin)]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<BulkImportRubricCriteriaResult>> ImportFile(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("Vui lòng chọn file Excel (.xlsx) hoặc file CSV.");

        await using var stream = file.OpenReadStream();
        var result = await criterionService.ImportFromStreamAsync(stream, file.FileName, ct);
        return Ok(result);
    }

    [HttpGet("template")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
    {
        var bytes = await criterionService.GenerateCriteriaTemplateExcelAsync(ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "rubric-criteria-template.xlsx");
    }

    [HttpGet("survey-results")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<SurveySummaryResponseDto>> GetSurveyResults([FromQuery] SurveyResultQuery query, CancellationToken ct)
        => Ok(await criterionService.GetSurveyResultsAsync(query, ct));

    [HttpGet("survey-results/options")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<SurveyFilterOptions>> GetSurveyFilterOptions(CancellationToken ct)
        => Ok(await criterionService.GetSurveyFilterOptionsAsync(ct));

    [HttpGet("survey-results/export-excel")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> ExportSurveyResults([FromQuery] SurveyResultQuery query, CancellationToken ct)
    {
        var bytes = await criterionService.ExportSurveyResultsToExcelAsync(query, ct);
        var fileName = $"PACE-Survey-Report-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx";
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpPost("survey-results/seed-sample")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<object>> SeedSample(CancellationToken ct)
    {
        int count = await criterionService.SeedSampleSurveysIfEmptyAsync(ct);
        return Ok(new { message = $"Đã tạo {count} đánh giá mẫu khảo sát cho dữ liệu hiện có.", seededCount = count });
    }

    [HttpPost("submit-survey")]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> SubmitSurvey([FromBody] SubmitSurveyRequest request, CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId))
            throw new UnauthorizedException("Phiên làm việc không hợp lệ.");

        await criterionService.SubmitSurveyAsync(request, userId, ct);
        return Ok(new { success = true, message = "Đã lưu kết quả khảo sát thành công." });
    }
}
