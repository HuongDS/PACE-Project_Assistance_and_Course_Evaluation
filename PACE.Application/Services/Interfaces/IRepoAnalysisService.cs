using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IRepoAnalysisService
{
    Task<AnalyzeResultDto> CreateAndAnalyzeAsync(CreateAnalysisRunRequest request, CancellationToken ct = default);
    /// <param name="regenerateTemplate">Tạo lại template nghiệp vụ từ code mới và gộp vào nghiệp vụ hiện có (không xoá gì).</param>
    Task<AnalyzeResultDto> ReanalyzeAsync(Guid analysisRunId, bool regenerateTemplate = false, CancellationToken ct = default);
}
