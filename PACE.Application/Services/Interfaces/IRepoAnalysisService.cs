using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IRepoAnalysisService
{
    Task<AnalyzeResultDto> CreateAndAnalyzeAsync(CreateAnalysisRunRequest request, bool force, CancellationToken ct = default);
}
