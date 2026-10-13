using PACE.BLL.Dtos;

namespace PACE.BLL.Services.Interfaces;

public interface IRepoAnalysisService
{
    Task<AnalyzeResultDto> CreateAndAnalyzeAsync(CreateAnalysisRunRequest request, bool force, CancellationToken ct = default);
}
