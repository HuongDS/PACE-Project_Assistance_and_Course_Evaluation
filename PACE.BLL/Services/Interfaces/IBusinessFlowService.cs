using PACE.BLL.Dtos;

namespace PACE.BLL.Services.Interfaces;

public interface IBusinessFlowService
{
    Task<IReadOnlyList<BusinessFlowSummaryDto>> GetByBusinessAsync(Guid businessId, CancellationToken ct = default);
    Task<BusinessFlowDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default);
}
