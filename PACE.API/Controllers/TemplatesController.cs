using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Analysis.Template;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/templates")]
[Authorize(Roles = Roles.Admin)]
public class TemplatesController(ITemplateGenerationService service) : ControllerBase
{
    /// <summary>Quét repo và trả về bản nháp template nghiệp vụ để admin xem, chỉnh sửa trước khi chốt.</summary>
    [HttpPost("generate")]
    public async Task<ActionResult<GeneratedTemplateDto>> Generate(GenerateTemplateRequest request, CancellationToken ct)
        => Ok(await service.GenerateAsync(request, ct));

    /// <summary>Nhận template đã chỉnh và trả về file template_business.json.</summary>
    [HttpPost("export")]
    public IActionResult Export(BusinessTemplateRequest request)
    {
        var businesses = BusinessTemplateLoader.Normalize(request.Businesses);
        var bytes = new UTF8Encoding(false).GetBytes(BusinessTemplateGenerator.Serialize(businesses));
        return File(bytes, "application/json", BusinessTemplateLoader.FileName);
    }
}
