using PACE.BLL.Dtos;

namespace PACE.BLL.Services.Interfaces;

public interface IBusinessService
{
    Task<IReadOnlyList<BusinessDto>> GetByRunAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<BusinessDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request, CancellationToken ct = default);
}
