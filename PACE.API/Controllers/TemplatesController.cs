using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Analysis.Template;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize(Roles = Roles.Admin)]
public class TemplatesController(ITemplateGenerationService service, IBatchTemplateService batchService) : ControllerBase
{
    /// <summary>Quét repo và trả về bản nháp template nghiệp vụ để admin xem, chỉnh sửa trước khi chốt.</summary>
    [HttpPost("generate")]
    public async Task<ActionResult<GeneratedTemplateDto>> Generate(GenerateTemplateRequest request, CancellationToken ct)
        => Ok(await service.GenerateAsync(request, ct));

    /// <summary>Đọc file Excel (.xlsx) chứa nhiều link repo và trả về bản nháp template của từng repo.</summary>
    [HttpPost("generate-from-excel")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<BatchTemplateResultDto>> GenerateFromExcel(
        IFormFile file, [FromQuery] bool useAi = true, [FromQuery] bool regenerate = false, CancellationToken ct = default)
        => Ok(await RunBatchAsync(file, useAi, regenerate, ct));

    /// <summary>Như generate-from-excel nhưng trả về file zip: mỗi repo một thư mục chứa template_business.json, kèm _report.txt.</summary>
    [HttpPost("generate-from-excel/download")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> DownloadFromExcel(
        IFormFile file, [FromQuery] bool useAi = true, [FromQuery] bool regenerate = false, CancellationToken ct = default)
    {
        var result = await RunBatchAsync(file, useAi, regenerate, ct);
        return File(TemplateArchiveBuilder.Build(result), "application/zip", "templates.zip");
    }

    private async Task<BatchTemplateResultDto> RunBatchAsync(IFormFile file, bool useAi, bool regenerate, CancellationToken ct)
    {
        if (file is null || file.Length == 0) throw new BadRequestException("Chưa chọn file Excel.");
        if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Chỉ hỗ trợ file .xlsx.");

        await using var stream = new MemoryStream();
        await file.CopyToAsync(stream, ct);
        stream.Position = 0;
        return await batchService.GenerateAsync(stream, useAi, regenerate, ct);
    }

    /// <summary>Nhận template đã chỉnh và trả về file template_business.json.</summary>
    [HttpPost("export")]
    public IActionResult Export(BusinessTemplateRequest request)
    {
        var businesses = BusinessTemplateLoader.Normalize(request.Businesses);
        var bytes = new UTF8Encoding(false).GetBytes(BusinessTemplateGenerator.Serialize(businesses));
        return File(bytes, "application/json", BusinessTemplateLoader.FileName);
    }
}
