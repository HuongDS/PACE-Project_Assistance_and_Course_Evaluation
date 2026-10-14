using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IAnalysisRunService
{
    Task<PagedResult<AnalysisRunDto>> GetPagedAsync(AnalysisRunQuery query, CancellationToken ct = default);
    Task<AnalysisRunDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<AnalysisRunDetailDto> GetMineAsync(CancellationToken ct = default);
    Task<AnalysisRunDto> UpdateAsync(Guid id, UpdateAnalysisRunRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
    Task<AnalysisRunTemplateDto> GetTemplateAsync(Guid id, CancellationToken ct = default);
    Task<AnalysisRunTemplateDto> SaveTemplateAsync(Guid id, BusinessTemplateRequest request, CancellationToken ct = default);
}
