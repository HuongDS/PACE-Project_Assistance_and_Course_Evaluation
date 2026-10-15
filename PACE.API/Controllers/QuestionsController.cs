using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Authorize(Roles = Roles.Admin)]
public class QuestionsController(IQuestionGenerationService questionService) : ControllerBase
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
}
