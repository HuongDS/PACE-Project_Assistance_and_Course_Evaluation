using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IBusinessFlowService
{
    Task<IReadOnlyList<BusinessFlowSummaryDto>> GetByBusinessAsync(Guid businessId, CancellationToken ct = default);
    Task<BusinessFlowDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default);
}
