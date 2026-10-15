using PACE.Application.Dtos;
using PACE.Application.Dtos.StudentAnswer;

namespace PACE.Application.Services.Interfaces;

public interface IBusinessService
{
    Task<IReadOnlyList<BusinessDto>> GetByRunAsync(Guid analysisRunId, CancellationToken ct = default);
    Task<BusinessDetailDto> GetDetailAsync(Guid id, CancellationToken ct = default);
    Task<BusinessDto> UpdateAsync(Guid id, UpdateBusinessRequest request, CancellationToken ct = default);
    Task<CodeFlowDto?> GetCodeBaseWithBusinessAsync(Guid businessId, CancellationToken ct = default);
}
