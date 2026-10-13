using PACE.BLL.Dtos;
using PACE.DAL.Repositories;

namespace PACE.BLL.Services.Interfaces;

public interface IAnalysisRunService
{
    Task<PagedResult<AnalysisRunDto>> GetPagedAsync(AnalysisRunQuery query, CancellationToken ct = default);
    Task<AnalysisRunDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<AnalysisRunDetailDto> GetMineAsync(CancellationToken ct = default);
    Task<AnalysisRunDto> UpdateAsync(Guid id, UpdateAnalysisRunRequest request, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
