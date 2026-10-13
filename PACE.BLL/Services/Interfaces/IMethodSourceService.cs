using PACE.BLL.Dtos;
using PACE.DAL.Repositories;

namespace PACE.BLL.Services.Interfaces;

public interface IMethodSourceService
{
    Task<PagedResult<MethodSourceSummaryDto>> GetPagedAsync(MethodSourceQuery query, CancellationToken ct = default);
    Task<MethodSourceDto> GetByIdAsync(Guid id, CancellationToken ct = default);
}
