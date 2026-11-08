using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.QuestionGeneration.Drawio;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
public class QuestionsController(
    IQuestionGenerationService questionService,
    IDrawioSpecificationStorage specStorage) : ControllerBase
{
    /// <summary>
    /// Sinh câu hỏi vấn đáp cho một nghiệp vụ và lưu vào DB.
    /// mode: Raw (mã nguồn thô) | Graph (sơ đồ Mermaid) | Hybrid (N_w &gt; 10 node → Graph, ngược lại Raw).
    /// </summary>
    [HttpPost("api/businesses/{businessId:guid}/questions/generate")]
    public async Task<ActionResult<GenerateQuestionsResponse>> Generate(Guid businessId, GenerateQuestionsRequest request, CancellationToken ct)
        => Ok(await questionService.GenerateAsync(businessId, request, ct));

    [HttpGet("api/businesses/{businessId:guid}/questions")]
    public async Task<ActionResult<IReadOnlyList<GeneratedQuestionDto>>> GetByBusiness(Guid businessId, CancellationToken ct)
        => Ok(await questionService.GetByBusinessAsync(businessId, ct));

    [HttpDelete("api/questions/{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await questionService.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>
    /// Upload và lưu sơ đồ quy trình chuẩn Draw.io (dưới dạng chuỗi JSON thô).
    /// </summary>
    [HttpPost("api/businesses/{businessId:guid}/specification/drawio")]
    public async Task<ActionResult<DrawioProcessDiagramDto>> SaveSpecification(
        Guid businessId,
        [FromBody] DrawioUploadRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.RawXml))
            throw new BadRequestException("Nội dung RawXml không được để trống.");

        var diagram = DrawioXmlParser.Parse(request.RawXml, businessId, request.DiagramName);
        await specStorage.SaveSpecificationAsync(businessId, diagram, ct);

        return Ok(new DrawioProcessDiagramDto(
            diagram.BusinessId,
            diagram.DiagramName,
            diagram.Nodes.Count,
            diagram.Edges.Count,
            diagram.ExecutionPaths.Count,
            diagram.NormalizedFlowText,
            diagram.ProcessedAt));
    }

    /// <summary>
    /// Upload file .xml hoặc .drawio quy trình chuẩn được export từ Draw.io.
    /// </summary>
    [HttpPost("api/businesses/{businessId:guid}/specification/drawio/file")]
    public async Task<ActionResult<DrawioProcessDiagramDto>> UploadSpecificationFile(
        Guid businessId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            throw new BadRequestException("Vui lòng đính kèm file Draw.io XML hợp lệ.");

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var rawXml = await reader.ReadToEndAsync(ct);

        var diagramName = Path.GetFileNameWithoutExtension(file.FileName);
        var diagram = DrawioXmlParser.Parse(rawXml, businessId, diagramName);
        await specStorage.SaveSpecificationAsync(businessId, diagram, ct);

        return Ok(new DrawioProcessDiagramDto(
            diagram.BusinessId,
            diagram.DiagramName,
            diagram.Nodes.Count,
            diagram.Edges.Count,
            diagram.ExecutionPaths.Count,
            diagram.NormalizedFlowText,
            diagram.ProcessedAt));
    }

    /// <summary>
    /// Lấy sơ đồ quy trình chuẩn hiện tại đã lưu cho nghiệp vụ.
    /// </summary>
    [HttpGet("api/businesses/{businessId:guid}/specification/drawio")]
    public async Task<ActionResult<DrawioProcessDiagramDto>> GetSpecification(Guid businessId, CancellationToken ct)
    {
        var diagram = await specStorage.GetSpecificationAsync(businessId, ct);
        if (diagram == null)
            throw new NotFoundException("sơ đồ quy trình chuẩn", businessId);

        return Ok(new DrawioProcessDiagramDto(
            diagram.BusinessId,
            diagram.DiagramName,
            diagram.Nodes.Count,
            diagram.Edges.Count,
            diagram.ExecutionPaths.Count,
            diagram.NormalizedFlowText,
            diagram.ProcessedAt));
    }

    /// <summary>
    /// Xoá sơ đồ quy trình chuẩn của nghiệp vụ.
    /// </summary>
    [HttpDelete("api/businesses/{businessId:guid}/specification/drawio")]
    public async Task<IActionResult> DeleteSpecification(Guid businessId, CancellationToken ct)
    {
        await specStorage.DeleteSpecificationAsync(businessId, ct);
        return NoContent();
    }
}
