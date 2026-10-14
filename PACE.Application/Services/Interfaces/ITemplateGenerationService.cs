using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface ITemplateGenerationService
{
    Task<GeneratedTemplateDto> GenerateAsync(GenerateTemplateRequest request, CancellationToken ct = default);
}
