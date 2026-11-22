using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PACE.Application.Common.Exceptions;
using PACE.Application.Dtos;
using PACE.Application.Services.Interfaces;
using PACE.Domain.Enums;

namespace PACE.API.Controllers;

[ApiController]
[Route("api/repos/{repoId:guid}")]
[Authorize]
public class RepoBusinessesController(IRepoBusinessService service) : ControllerBase
{
    private const long MaxDiagramFileBytes = 2_000_000;
    private static readonly string[] AllowedDiagramExtensions = [".drawio", ".xml"];

    [HttpGet("workspace")]
    public async Task<ActionResult<RepoBusinessWorkspaceDto>> GetWorkspace(Guid repoId, CancellationToken ct)
        => Ok(await service.GetWorkspaceAsync(repoId, ct));

    [HttpGet("apis")]
    public async Task<ActionResult<IReadOnlyList<RepoApiDto>>> GetApis(Guid repoId, CancellationToken ct)
        => Ok(await service.GetApisAsync(repoId, ct));

    [HttpPost("reload")]
    [Authorize(Roles = Roles.Student + "," + Roles.Admin)]
    public async Task<ActionResult<AnalyzeResultDto>> ReloadSource(Guid repoId)
        => Ok(await service.ReloadSourceAsync(repoId, CancellationToken.None));

    [HttpGet("businesses/{businessId:guid}")]
    public async Task<ActionResult<RepoBusinessDto>> GetBusiness(Guid repoId, Guid businessId, CancellationToken ct)
        => Ok(await service.GetBusinessAsync(repoId, businessId, ct));

    [HttpPost("businesses")]
    [Authorize(Roles = Roles.Student)]
    public async Task<ActionResult<RepoBusinessDto>> CreateBusiness(Guid repoId, SaveRepoBusinessRequest request, CancellationToken ct)
    {
        var business = await service.CreateBusinessAsync(repoId, request, ct);
        return CreatedAtAction(nameof(GetBusiness), new { repoId, businessId = business.Id }, business);
    }

    [HttpPut("businesses/{businessId:guid}")]
    [Authorize(Roles = Roles.Student)]
    public async Task<ActionResult<RepoBusinessDto>> UpdateBusiness(Guid repoId, Guid businessId, SaveRepoBusinessRequest request, CancellationToken ct)
        => Ok(await service.UpdateBusinessAsync(repoId, businessId, request, ct));

    [HttpDelete("businesses/{businessId:guid}")]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> DeleteBusiness(Guid repoId, Guid businessId, CancellationToken ct)
    {
        await service.DeleteBusinessAsync(repoId, businessId, ct);
        return NoContent();
    }

    [HttpGet("businesses/{businessId:guid}/diagrams/{diagramId:guid}")]
    public async Task<ActionResult<BusinessDiagramDto>> GetDiagram(Guid repoId, Guid businessId, Guid diagramId, CancellationToken ct)
        => Ok(await service.GetDiagramAsync(repoId, businessId, diagramId, ct));

    [HttpPost("businesses/{businessId:guid}/diagrams")]
    [Authorize(Roles = Roles.Student)]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<BusinessDiagramDto>> CreateDiagram(Guid repoId, Guid businessId, SaveBusinessDiagramRequest request, CancellationToken ct)
    {
        var diagram = await service.CreateDiagramAsync(repoId, businessId, request, ct);
        return CreatedAtAction(nameof(GetDiagram), new { repoId, businessId, diagramId = diagram.Diagram.Id }, diagram);
    }

    [HttpPost("businesses/{businessId:guid}/diagrams/file")]
    [Authorize(Roles = Roles.Student)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<BusinessDiagramDto>> UploadDiagramFile(
        Guid repoId,
        Guid businessId,
        IFormFile file,
        [FromForm] string? title,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("Vui lòng đính kèm file draw.io.");
        if (file.Length > MaxDiagramFileBytes)
            throw new BadRequestException("File draw.io quá lớn, tối đa 2 MB.");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedDiagramExtensions.Contains(extension))
            throw new BadRequestException("Chỉ nhận file .drawio hoặc .xml xuất từ draw.io.");

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var request = new SaveBusinessDiagramRequest
        {
            Title = string.IsNullOrWhiteSpace(title) ? Path.GetFileNameWithoutExtension(file.FileName) : title.Trim(),
            Format = DiagramFormat.Drawio,
            SourceXml = await reader.ReadToEndAsync(ct)
        };

        var diagram = await service.CreateDiagramAsync(repoId, businessId, request, ct);
        return CreatedAtAction(nameof(GetDiagram), new { repoId, businessId, diagramId = diagram.Diagram.Id }, diagram);
    }

    [HttpPut("businesses/{businessId:guid}/diagrams/{diagramId:guid}")]
    [Authorize(Roles = Roles.Student)]
    [RequestSizeLimit(5_000_000)]
    public async Task<ActionResult<BusinessDiagramDto>> UpdateDiagram(Guid repoId, Guid businessId, Guid diagramId, SaveBusinessDiagramRequest request, CancellationToken ct)
        => Ok(await service.UpdateDiagramAsync(repoId, businessId, diagramId, request, ct));

    [HttpDelete("businesses/{businessId:guid}/diagrams/{diagramId:guid}")]
    [Authorize(Roles = Roles.Student)]
    public async Task<IActionResult> DeleteDiagram(Guid repoId, Guid businessId, Guid diagramId, CancellationToken ct)
    {
        await service.DeleteDiagramAsync(repoId, businessId, diagramId, ct);
        return NoContent();
    }
}
