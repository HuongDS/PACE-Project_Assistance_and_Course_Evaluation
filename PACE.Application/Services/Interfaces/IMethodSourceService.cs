using PACE.Application.Common.Models;
using PACE.Application.Dtos;

namespace PACE.Application.Services.Interfaces;

public interface IMethodSourceService
{
    Task<PagedResult<MethodSourceSummaryDto>> GetPagedAsync(MethodSourceQuery query, CancellationToken ct = default);
    Task<MethodSourceDto> GetByIdAsync(Guid id, CancellationToken ct = default);
}
